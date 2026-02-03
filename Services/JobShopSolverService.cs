using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for solving Job Shop Scheduling problems.
/// Provides heuristic solvers that run in WebAssembly, and interfaces for external solvers.
///
/// For optimal solutions, use the companion JobShopORToolsSolver console application
/// which uses Google OR-Tools CP-SAT solver.
/// </summary>
public class JobShopSolverService
{
    private readonly JobShopValidatorService _validator = new();

    /// <summary>
    /// Available heuristic algorithms
    /// </summary>
    public enum Algorithm
    {
        /// <summary>Shortest Processing Time first</summary>
        SPT,
        /// <summary>Longest Processing Time first</summary>
        LPT,
        /// <summary>First Come First Served</summary>
        FCFS,
        /// <summary>Earliest Due Date (for weighted problems)</summary>
        EDD,
        /// <summary>Most Work Remaining first</summary>
        MWR,
        /// <summary>Least Work Remaining first</summary>
        LWR,
        /// <summary>Random priority</summary>
        Random
    }

    /// <summary>
    /// Solves the job shop instance using the specified heuristic algorithm.
    /// </summary>
    public JobShopSolverResult Solve(JobShopInstance instance, Algorithm algorithm = Algorithm.SPT, int? seed = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Validate instance first
        var instanceErrors = instance.Validate();
        if (instanceErrors.Count > 0)
        {
            return new JobShopSolverResult
            {
                Success = false,
                Status = JobShopSolverStatus.Error,
                ErrorMessage = string.Join("; ", instanceErrors)
            };
        }

        try
        {
            JobShopSolution solution = algorithm switch
            {
                Algorithm.SPT => SolveSPT(instance),
                Algorithm.LPT => SolveLPT(instance),
                Algorithm.FCFS => SolveFCFS(instance),
                Algorithm.MWR => SolveMWR(instance),
                Algorithm.LWR => SolveLWR(instance),
                Algorithm.Random => SolveRandom(instance, seed ?? Environment.TickCount),
                _ => SolveSPT(instance)
            };

            stopwatch.Stop();

            solution.Status = JobShopSolverStatus.Feasible;
            solution.Solver = $"Heuristic ({algorithm})";
            solution.SolveTime = stopwatch.Elapsed.TotalSeconds;
            solution.Makespan = solution.CalculateMakespan();

            // Validate the solution
            var validation = _validator.Validate(instance, solution);

            return new JobShopSolverResult
            {
                Success = validation.IsValid,
                Solution = solution,
                Status = validation.IsValid ? JobShopSolverStatus.Feasible : JobShopSolverStatus.Error,
                SolveTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                Validation = validation,
                ErrorMessage = validation.IsValid ? null : string.Join("; ", validation.Errors)
            };
        }
        catch (Exception ex)
        {
            return new JobShopSolverResult
            {
                Success = false,
                Status = JobShopSolverStatus.Error,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Solves using SPT (Shortest Processing Time) dispatch rule.
    /// At each decision point, selects the operation with shortest processing time.
    /// </summary>
    private JobShopSolution SolveSPT(JobShopInstance instance)
    {
        return SolveWithPriority(instance, (job, op, remaining, duration) => duration);
    }

    /// <summary>
    /// Solves using LPT (Longest Processing Time) dispatch rule.
    /// At each decision point, selects the operation with longest processing time.
    /// </summary>
    private JobShopSolution SolveLPT(JobShopInstance instance)
    {
        return SolveWithPriority(instance, (job, op, remaining, duration) => -duration);
    }

    /// <summary>
    /// Solves using FCFS (First Come First Served) dispatch rule.
    /// Processes jobs in order of their index.
    /// </summary>
    private JobShopSolution SolveFCFS(JobShopInstance instance)
    {
        return SolveWithPriority(instance, (job, op, remaining, duration) => job * 1000 + op);
    }

    /// <summary>
    /// Solves using MWR (Most Work Remaining) dispatch rule.
    /// Prioritizes operations from jobs with the most remaining processing time.
    /// </summary>
    private JobShopSolution SolveMWR(JobShopInstance instance)
    {
        return SolveWithPriority(instance, (job, op, remaining, duration) => -remaining);
    }

    /// <summary>
    /// Solves using LWR (Least Work Remaining) dispatch rule.
    /// Prioritizes operations from jobs with the least remaining processing time.
    /// </summary>
    private JobShopSolution SolveLWR(JobShopInstance instance)
    {
        return SolveWithPriority(instance, (job, op, remaining, duration) => remaining);
    }

    /// <summary>
    /// Solves using random priority (for testing/comparison).
    /// </summary>
    private JobShopSolution SolveRandom(JobShopInstance instance, int seed)
    {
        var random = new Random(seed);
        return SolveWithPriority(instance, (job, op, remaining, duration) => random.Next());
    }

    /// <summary>
    /// Generic priority-based dispatch solver.
    /// </summary>
    private JobShopSolution SolveWithPriority(
        JobShopInstance instance,
        Func<int, int, int, int, int> priorityFunc)
    {
        var solution = JobShopSolution.FromInstance(instance);
        int numJobs = instance.Data.Length;
        int numMachines = instance.MachineCount;

        // Track next operation index for each job
        var nextOp = new int[numJobs];

        // Track when each machine becomes available
        var machineAvailable = new int[numMachines];

        // Track when each job can continue (after its previous operation)
        var jobAvailable = new int[numJobs];

        // Calculate remaining work for each job
        var remainingWork = new int[numJobs];
        for (int j = 0; j < numJobs; j++)
        {
            remainingWork[j] = instance.Data[j].Sum(op => op[1]);
        }

        // Total operations to schedule
        int totalOps = instance.TotalOperationCount;
        int scheduled = 0;

        while (scheduled < totalOps)
        {
            // Find all ready operations (next operation for each job that hasn't finished)
            var ready = new List<(int Job, int Op, int Machine, int Duration, int Priority)>();

            for (int j = 0; j < numJobs; j++)
            {
                if (nextOp[j] < instance.Data[j].Length)
                {
                    int op = nextOp[j];
                    int machine = instance.GetMachineId(j, op);
                    int duration = instance.GetProcessingTime(j, op);
                    int priority = priorityFunc(j, op, remainingWork[j], duration);
                    ready.Add((j, op, machine, duration, priority));
                }
            }

            if (ready.Count == 0) break;

            // Sort by priority, then by earliest available time
            ready = ready
                .OrderBy(r => r.Priority)
                .ThenBy(r => Math.Max(machineAvailable[r.Machine], jobAvailable[r.Job]))
                .ToList();

            // Schedule the highest priority operation
            var selected = ready[0];
            int startTime = Math.Max(
                machineAvailable[selected.Machine],
                jobAvailable[selected.Job]
            );
            int endTime = startTime + selected.Duration;

            // Update solution
            solution.Data[selected.Job][selected.Op] = new[]
            {
                selected.Machine,
                selected.Duration,
                startTime
            };

            // Update tracking
            machineAvailable[selected.Machine] = endTime;
            jobAvailable[selected.Job] = endTime;
            remainingWork[selected.Job] -= selected.Duration;
            nextOp[selected.Job]++;
            scheduled++;
        }

        return solution;
    }

    /// <summary>
    /// Tries multiple algorithms and returns the best solution.
    /// </summary>
    public JobShopSolverResult SolveMultiple(JobShopInstance instance, IEnumerable<Algorithm>? algorithms = null)
    {
        var algos = algorithms ?? new[] { Algorithm.SPT, Algorithm.LPT, Algorithm.MWR, Algorithm.LWR };

        JobShopSolverResult? best = null;

        foreach (var algo in algos)
        {
            var result = Solve(instance, algo);
            if (result.Success && result.Solution != null)
            {
                if (best?.Solution == null || result.Solution.Makespan < best.Solution.Makespan)
                {
                    best = result;
                }
            }
        }

        return best ?? new JobShopSolverResult
        {
            Success = false,
            Status = JobShopSolverStatus.Error,
            ErrorMessage = "All algorithms failed"
        };
    }

    /// <summary>
    /// Compresses a solution by moving each operation as early as possible
    /// while respecting constraints. This is a post-processing improvement step.
    /// </summary>
    public JobShopSolution CompressSolution(JobShopInstance instance, JobShopSolution solution)
    {
        var compressed = JobShopSolution.FromInstance(instance);
        compressed.Name = solution.Name + "_compressed";
        compressed.Solver = solution.Solver + " + Compression";

        int numJobs = solution.Data.Length;
        int numMachines = solution.MachineCount;

        // Get operations sorted by their current start time
        var operations = new List<(int Job, int Op, int Machine, int Duration, int Start)>();
        for (int j = 0; j < numJobs; j++)
        {
            for (int o = 0; o < solution.Data[j].Length; o++)
            {
                operations.Add((
                    j, o,
                    solution.GetMachineId(j, o),
                    solution.GetProcessingTime(j, o),
                    solution.GetStartTime(j, o)
                ));
            }
        }

        // Sort by current start time
        operations = operations.OrderBy(op => op.Start).ToList();

        // Track machine availability
        var machineAvailable = new int[numMachines];
        var jobOpEnd = new int[numJobs]; // End time of last scheduled op for each job

        foreach (var op in operations)
        {
            // Earliest start respecting precedence
            int earliestJob = op.Op > 0 ? jobOpEnd[op.Job] : 0;

            // Earliest start respecting machine
            int earliestMachine = machineAvailable[op.Machine];

            // Actual start is max of both
            int start = Math.Max(earliestJob, earliestMachine);
            int end = start + op.Duration;

            // Update solution
            compressed.Data[op.Job][op.Op] = new[] { op.Machine, op.Duration, start };

            // Update tracking
            machineAvailable[op.Machine] = end;
            jobOpEnd[op.Job] = end;
        }

        compressed.Makespan = compressed.CalculateMakespan();
        return compressed;
    }
}

/// <summary>
/// Result of a job shop solve operation
/// </summary>
public class JobShopSolverResult
{
    public bool Success { get; set; }
    public JobShopSolution? Solution { get; set; }
    public string Status { get; set; } = JobShopSolverStatus.Unknown;
    public double SolveTimeSeconds { get; set; }
    public string? ErrorMessage { get; set; }
    public ValidationResult? Validation { get; set; }

    /// <summary>Makespan of the solution (if valid)</summary>
    public int Makespan => Solution?.Makespan ?? 0;
}
