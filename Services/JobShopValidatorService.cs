using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for validating Job Shop Scheduling solutions.
/// Checks precedence constraints, machine conflicts, and other feasibility conditions.
/// </summary>
public class JobShopValidatorService
{
    /// <summary>
    /// Validates a solution against an instance.
    /// </summary>
    /// <param name="instance">The problem instance</param>
    /// <param name="solution">The proposed solution</param>
    /// <returns>Validation result with errors if any</returns>
    public ValidationResult Validate(JobShopInstance instance, JobShopSolution solution)
    {
        var result = new ValidationResult();

        // Basic structure validation
        ValidateStructure(instance, solution, result);
        if (!result.IsValid) return result;

        // Check all operations are present with correct machine and duration
        ValidateOperations(instance, solution, result);

        // Check precedence constraints (operations within job are sequential)
        ValidatePrecedenceConstraints(solution, result);

        // Check machine conflicts (no overlapping operations on same machine)
        ValidateMachineConstraints(solution, result);

        // Check non-negative start times
        ValidateStartTimes(solution, result);

        // Calculate actual makespan
        result.ComputedMakespan = solution.CalculateMakespan();

        // Verify reported makespan
        if (solution.Makespan > 0 && solution.Makespan != result.ComputedMakespan)
        {
            result.Warnings.Add($"Reported makespan ({solution.Makespan}) differs from computed makespan ({result.ComputedMakespan})");
        }

        // Calculate metrics
        CalculateMetrics(instance, solution, result);

        return result;
    }

    /// <summary>
    /// Validates a solution JSON against an instance JSON.
    /// </summary>
    public ValidationResult ValidateJson(string instanceJson, string solutionJson)
    {
        var instance = JobShopInstance.FromJson(instanceJson);
        if (instance == null)
        {
            return new ValidationResult
            {
                Errors = { "Failed to parse instance JSON" }
            };
        }

        var solution = JobShopSolution.FromJson(solutionJson);
        if (solution == null)
        {
            return new ValidationResult
            {
                Errors = { "Failed to parse solution JSON" }
            };
        }

        return Validate(instance, solution);
    }

    private void ValidateStructure(JobShopInstance instance, JobShopSolution solution, ValidationResult result)
    {
        if (instance.Data.Length != solution.Data.Length)
        {
            result.Errors.Add($"Job count mismatch: instance has {instance.Data.Length} jobs, solution has {solution.Data.Length}");
        }

        for (int j = 0; j < Math.Min(instance.Data.Length, solution.Data.Length); j++)
        {
            if (instance.Data[j].Length != solution.Data[j].Length)
            {
                result.Errors.Add($"Job {j + 1}: operation count mismatch - instance has {instance.Data[j].Length}, solution has {solution.Data[j].Length}");
            }
        }
    }

    private void ValidateOperations(JobShopInstance instance, JobShopSolution solution, ValidationResult result)
    {
        for (int j = 0; j < instance.Data.Length && j < solution.Data.Length; j++)
        {
            for (int o = 0; o < instance.Data[j].Length && o < solution.Data[j].Length; o++)
            {
                int instanceMachine = instance.GetMachineId(j, o);
                int solutionMachine = solution.GetMachineId(j, o);
                int instanceDuration = instance.GetProcessingTime(j, o);
                int solutionDuration = solution.GetProcessingTime(j, o);

                if (instanceMachine != solutionMachine)
                {
                    result.Errors.Add($"Job {j + 1}, Op {o + 1}: Machine mismatch - expected {instanceMachine}, got {solutionMachine}");
                }

                if (instanceDuration != solutionDuration)
                {
                    result.Errors.Add($"Job {j + 1}, Op {o + 1}: Duration mismatch - expected {instanceDuration}, got {solutionDuration}");
                }

                // Validate operation has start time
                if (solution.Data[j][o].Length < 3)
                {
                    result.Errors.Add($"Job {j + 1}, Op {o + 1}: Missing start time in solution");
                }
            }
        }
    }

    private void ValidatePrecedenceConstraints(JobShopSolution solution, ValidationResult result)
    {
        for (int j = 0; j < solution.Data.Length; j++)
        {
            for (int o = 1; o < solution.Data[j].Length; o++)
            {
                int prevEnd = solution.GetEndTime(j, o - 1);
                int currStart = solution.GetStartTime(j, o);

                if (currStart < prevEnd)
                {
                    result.Errors.Add(
                        $"Precedence violation: Job {j + 1}, Op {o + 1} starts at {currStart} " +
                        $"but previous operation ends at {prevEnd}"
                    );
                    result.PrecedenceViolations.Add(new PrecedenceViolationInfo
                    {
                        JobIndex = j,
                        OperationIndex = o,
                        PreviousEndTime = prevEnd,
                        CurrentStartTime = currStart,
                        Gap = currStart - prevEnd
                    });
                }
            }
        }
    }

    private void ValidateMachineConstraints(JobShopSolution solution, ValidationResult result)
    {
        // Group operations by machine
        var operationsByMachine = new Dictionary<int, List<(int Job, int Op, int Start, int End)>>();

        for (int j = 0; j < solution.Data.Length; j++)
        {
            for (int o = 0; o < solution.Data[j].Length; o++)
            {
                int machine = solution.GetMachineId(j, o);
                int start = solution.GetStartTime(j, o);
                int end = solution.GetEndTime(j, o);

                if (!operationsByMachine.ContainsKey(machine))
                {
                    operationsByMachine[machine] = new List<(int, int, int, int)>();
                }
                operationsByMachine[machine].Add((j, o, start, end));
            }
        }

        // Check for overlaps on each machine
        foreach (var (machine, operations) in operationsByMachine)
        {
            var sorted = operations.OrderBy(op => op.Start).ToList();

            for (int i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];

                if (curr.Start < prev.End)
                {
                    result.Errors.Add(
                        $"Machine conflict on Machine {machine}: " +
                        $"Job {prev.Job + 1} Op {prev.Op + 1} [{prev.Start}-{prev.End}] overlaps with " +
                        $"Job {curr.Job + 1} Op {curr.Op + 1} [{curr.Start}-{curr.End}]"
                    );
                    result.MachineConflicts.Add(new MachineConflictInfo
                    {
                        MachineIndex = machine,
                        Operation1 = (prev.Job, prev.Op, prev.Start, prev.End),
                        Operation2 = (curr.Job, curr.Op, curr.Start, curr.End),
                        OverlapAmount = prev.End - curr.Start
                    });
                }
            }
        }
    }

    private void ValidateStartTimes(JobShopSolution solution, ValidationResult result)
    {
        for (int j = 0; j < solution.Data.Length; j++)
        {
            for (int o = 0; o < solution.Data[j].Length; o++)
            {
                int start = solution.GetStartTime(j, o);
                if (start < 0)
                {
                    result.Errors.Add($"Job {j + 1}, Op {o + 1}: Negative start time ({start})");
                }
            }
        }
    }

    private void CalculateMetrics(JobShopInstance instance, JobShopSolution solution, ValidationResult result)
    {
        // Total processing time
        int totalProcessingTime = 0;
        for (int j = 0; j < solution.Data.Length; j++)
        {
            for (int o = 0; o < solution.Data[j].Length; o++)
            {
                totalProcessingTime += solution.GetProcessingTime(j, o);
            }
        }

        // Machine utilization
        var machineWorkTime = new Dictionary<int, int>();
        for (int j = 0; j < solution.Data.Length; j++)
        {
            for (int o = 0; o < solution.Data[j].Length; o++)
            {
                int machine = solution.GetMachineId(j, o);
                int duration = solution.GetProcessingTime(j, o);
                if (!machineWorkTime.ContainsKey(machine))
                    machineWorkTime[machine] = 0;
                machineWorkTime[machine] += duration;
            }
        }

        double avgUtilization = result.ComputedMakespan > 0
            ? machineWorkTime.Values.Average() / (double)result.ComputedMakespan * 100
            : 0;

        // Job completion times and flow times
        var jobCompletionTimes = new int[solution.Data.Length];
        for (int j = 0; j < solution.Data.Length; j++)
        {
            if (solution.Data[j].Length > 0)
            {
                int lastOp = solution.Data[j].Length - 1;
                jobCompletionTimes[j] = solution.GetEndTime(j, lastOp);
            }
        }

        double avgFlowTime = jobCompletionTimes.Average();

        result.Metrics = new SolutionMetrics
        {
            Makespan = result.ComputedMakespan,
            LowerBound = instance.CalculateLowerBound(),
            TotalProcessingTime = totalProcessingTime,
            AverageMachineUtilization = avgUtilization,
            AverageFlowTime = avgFlowTime,
            MaxFlowTime = jobCompletionTimes.Max(),
            TotalIdleTime = (result.ComputedMakespan * instance.MachineCount) - totalProcessingTime
        };
    }
}

/// <summary>
/// Result of solution validation
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public int ComputedMakespan { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<PrecedenceViolationInfo> PrecedenceViolations { get; set; } = new();
    public List<MachineConflictInfo> MachineConflicts { get; set; } = new();
    public SolutionMetrics? Metrics { get; set; }

    /// <summary>Returns a summary string</summary>
    public string GetSummary()
    {
        if (IsValid)
        {
            return $"Valid solution with makespan {ComputedMakespan}";
        }
        return $"Invalid solution: {Errors.Count} error(s), {Warnings.Count} warning(s)";
    }
}

/// <summary>
/// Information about a precedence constraint violation
/// </summary>
public class PrecedenceViolationInfo
{
    public int JobIndex { get; set; }
    public int OperationIndex { get; set; }
    public int PreviousEndTime { get; set; }
    public int CurrentStartTime { get; set; }
    public int Gap { get; set; }
}

/// <summary>
/// Information about a machine conflict (overlapping operations)
/// </summary>
public class MachineConflictInfo
{
    public int MachineIndex { get; set; }
    public (int Job, int Op, int Start, int End) Operation1 { get; set; }
    public (int Job, int Op, int Start, int End) Operation2 { get; set; }
    public int OverlapAmount { get; set; }
}

/// <summary>
/// Metrics calculated from a solution
/// </summary>
public class SolutionMetrics
{
    public int Makespan { get; set; }
    public int LowerBound { get; set; }
    public int TotalProcessingTime { get; set; }
    public double AverageMachineUtilization { get; set; }
    public double AverageFlowTime { get; set; }
    public int MaxFlowTime { get; set; }
    public int TotalIdleTime { get; set; }

    /// <summary>Gap from lower bound as percentage</summary>
    public double GapPercentage => LowerBound > 0
        ? ((Makespan - LowerBound) / (double)LowerBound) * 100
        : 0;
}
