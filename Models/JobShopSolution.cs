using System.Text.Json;
using System.Text.Json.Serialization;

namespace dfd2wasm.Models;

/// <summary>
/// Represents a solution to a Job Shop Scheduling Problem (JSSP).
/// Contains start times for all operations in addition to machine and duration data.
/// </summary>
public class JobShopSolution
{
    /// <summary>Solution name/identifier</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "JobShop_Solution";

    /// <summary>Optional description</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Number of machines</summary>
    [JsonPropertyName("machines")]
    public int MachineCount { get; set; }

    /// <summary>Number of jobs</summary>
    [JsonPropertyName("jobs")]
    public int JobCount { get; set; }

    /// <summary>Time unit for durations (e.g., "minutes", "hours")</summary>
    [JsonPropertyName("time_unit")]
    public string TimeUnit { get; set; } = "minutes";

    /// <summary>Makespan (completion time of last operation)</summary>
    [JsonPropertyName("makespan")]
    public int Makespan { get; set; }

    /// <summary>Solver status (e.g., "OPTIMAL", "FEASIBLE", "INFEASIBLE")</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "UNKNOWN";

    /// <summary>Solver name/version</summary>
    [JsonPropertyName("solver")]
    public string? Solver { get; set; }

    /// <summary>Solve time in seconds</summary>
    [JsonPropertyName("solve_time")]
    public double? SolveTime { get; set; }

    /// <summary>Optional machine names</summary>
    [JsonPropertyName("machine_names")]
    public string[]? MachineNames { get; set; }

    /// <summary>Optional job names</summary>
    [JsonPropertyName("job_names")]
    public string[]? JobNames { get; set; }

    /// <summary>
    /// Solution data: array of jobs, each job is array of operations.
    /// Each operation is [machine_id, processing_time, start_time].
    /// </summary>
    [JsonPropertyName("data")]
    public int[][][] Data { get; set; } = Array.Empty<int[][]>();

    /// <summary>Gets machine ID for a specific operation</summary>
    public int GetMachineId(int jobIndex, int operationIndex) =>
        Data[jobIndex][operationIndex][0];

    /// <summary>Gets processing time for a specific operation</summary>
    public int GetProcessingTime(int jobIndex, int operationIndex) =>
        Data[jobIndex][operationIndex][1];

    /// <summary>Gets start time for a specific operation</summary>
    public int GetStartTime(int jobIndex, int operationIndex) =>
        Data[jobIndex][operationIndex].Length >= 3 ? Data[jobIndex][operationIndex][2] : 0;

    /// <summary>Gets end time for a specific operation</summary>
    public int GetEndTime(int jobIndex, int operationIndex) =>
        GetStartTime(jobIndex, operationIndex) + GetProcessingTime(jobIndex, operationIndex);

    /// <summary>Gets machine name (or default name if not specified)</summary>
    public string GetMachineName(int machineIndex) =>
        MachineNames != null && machineIndex < MachineNames.Length
            ? MachineNames[machineIndex]
            : $"Machine {machineIndex + 1}";

    /// <summary>Gets job name (or default name if not specified)</summary>
    public string GetJobName(int jobIndex) =>
        JobNames != null && jobIndex < JobNames.Length
            ? JobNames[jobIndex]
            : $"Job {jobIndex + 1}";

    /// <summary>Calculates the actual makespan from the solution data</summary>
    public int CalculateMakespan()
    {
        int maxEnd = 0;
        for (int j = 0; j < Data.Length; j++)
        {
            for (int o = 0; o < Data[j].Length; o++)
            {
                int endTime = GetEndTime(j, o);
                maxEnd = Math.Max(maxEnd, endTime);
            }
        }
        return maxEnd;
    }

    /// <summary>Creates a solution from an instance (with zero start times)</summary>
    public static JobShopSolution FromInstance(JobShopInstance instance)
    {
        var solution = new JobShopSolution
        {
            Name = instance.Name + "_solution",
            MachineCount = instance.MachineCount,
            JobCount = instance.JobCount,
            TimeUnit = instance.TimeUnit,
            MachineNames = instance.MachineNames,
            JobNames = instance.JobNames,
            Data = new int[instance.Data.Length][][]
        };

        for (int j = 0; j < instance.Data.Length; j++)
        {
            solution.Data[j] = new int[instance.Data[j].Length][];
            for (int o = 0; o < instance.Data[j].Length; o++)
            {
                int machine = instance.Data[j][o][0];
                int duration = instance.Data[j][o][1];
                solution.Data[j][o] = new[] { machine, duration, 0 }; // Start at 0 initially
            }
        }

        return solution;
    }

    /// <summary>Creates a solution from JSON string</summary>
    public static JobShopSolution? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<JobShopSolution>(json, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Serializes to JSON string</summary>
    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }
}

/// <summary>
/// Status codes for job shop solver results (string constants for JSON)
/// </summary>
public static class JobShopSolverStatus
{
    public const string Optimal = "OPTIMAL";
    public const string Feasible = "FEASIBLE";
    public const string Infeasible = "INFEASIBLE";
    public const string Unknown = "UNKNOWN";
    public const string Timeout = "TIMEOUT";
    public const string Error = "ERROR";
}
