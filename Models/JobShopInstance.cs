using System.Text.Json;
using System.Text.Json.Serialization;

namespace dfd2wasm.Models;

/// <summary>
/// Represents a Job Shop Scheduling Problem (JSSP) instance.
/// Each job consists of a sequence of operations that must be processed on specific machines.
/// Operations within a job must be executed in order (precedence constraints).
/// Each machine can only process one operation at a time (machine constraints).
/// </summary>
public class JobShopInstance
{
    /// <summary>Instance name/identifier</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "JobShop";

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

    /// <summary>Optional machine names</summary>
    [JsonPropertyName("machine_names")]
    public string[]? MachineNames { get; set; }

    /// <summary>Optional job names</summary>
    [JsonPropertyName("job_names")]
    public string[]? JobNames { get; set; }

    /// <summary>
    /// Job data: array of jobs, each job is array of operations.
    /// Each operation is [machine_id, processing_time].
    /// </summary>
    [JsonPropertyName("data")]
    public int[][][] Data { get; set; } = Array.Empty<int[][]>();

    /// <summary>Gets the number of operations in a specific job</summary>
    public int GetOperationCount(int jobIndex) =>
        jobIndex >= 0 && jobIndex < Data.Length ? Data[jobIndex].Length : 0;

    /// <summary>Gets the total number of operations across all jobs</summary>
    public int TotalOperationCount => Data.Sum(job => job.Length);

    /// <summary>Gets machine ID for a specific operation</summary>
    public int GetMachineId(int jobIndex, int operationIndex) =>
        Data[jobIndex][operationIndex][0];

    /// <summary>Gets processing time for a specific operation</summary>
    public int GetProcessingTime(int jobIndex, int operationIndex) =>
        Data[jobIndex][operationIndex][1];

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

    /// <summary>Calculates a lower bound on the makespan (max of job lengths and machine loads)</summary>
    public int CalculateLowerBound()
    {
        // Lower bound 1: Longest job processing time
        int maxJobTime = 0;
        for (int j = 0; j < Data.Length; j++)
        {
            int jobTime = Data[j].Sum(op => op[1]);
            maxJobTime = Math.Max(maxJobTime, jobTime);
        }

        // Lower bound 2: Maximum machine load
        var machineLoads = new int[MachineCount];
        for (int j = 0; j < Data.Length; j++)
        {
            for (int o = 0; o < Data[j].Length; o++)
            {
                int machine = Data[j][o][0];
                int duration = Data[j][o][1];
                if (machine >= 0 && machine < MachineCount)
                {
                    machineLoads[machine] += duration;
                }
            }
        }
        int maxMachineLoad = machineLoads.Max();

        return Math.Max(maxJobTime, maxMachineLoad);
    }

    /// <summary>Validates the instance data</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (MachineCount <= 0)
            errors.Add("Machine count must be positive");

        if (JobCount <= 0)
            errors.Add("Job count must be positive");

        if (Data == null || Data.Length == 0)
        {
            errors.Add("No job data provided");
            return errors;
        }

        if (Data.Length != JobCount)
            errors.Add($"Data has {Data.Length} jobs but JobCount is {JobCount}");

        for (int j = 0; j < Data.Length; j++)
        {
            if (Data[j] == null || Data[j].Length == 0)
            {
                errors.Add($"Job {j + 1} has no operations");
                continue;
            }

            for (int o = 0; o < Data[j].Length; o++)
            {
                if (Data[j][o] == null || Data[j][o].Length < 2)
                {
                    errors.Add($"Job {j + 1}, Op {o + 1}: Invalid operation format (expected [machine, duration])");
                    continue;
                }

                int machine = Data[j][o][0];
                int duration = Data[j][o][1];

                if (machine < 0 || machine >= MachineCount)
                    errors.Add($"Job {j + 1}, Op {o + 1}: Machine {machine} out of range [0, {MachineCount - 1}]");

                if (duration <= 0)
                    errors.Add($"Job {j + 1}, Op {o + 1}: Duration must be positive, got {duration}");
            }
        }

        return errors;
    }

    /// <summary>Creates an instance from JSON string</summary>
    public static JobShopInstance? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<JobShopInstance>(json, options);
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
