using System.Text.Json;
using System.Text.Json.Serialization;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for importing and exporting Gantt job-shop scheduling data in various formats.
/// Supports JSON format for job-shop scheduling problems (JSSP) and solutions.
/// </summary>
public class GanttImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region JSON Import

    /// <summary>
    /// Import Gantt job-shop scheduling data from JSON format.
    /// Supports two formats:
    /// - Problem format: [machine_id, duration] pairs
    /// - Solution format: [machine_id, duration, start_time] triples
    /// </summary>
    public GanttImportResult ImportFromJson(string json, int startingNodeId = 1, int startingEdgeId = 1)
    {
        var result = new GanttImportResult();

        try
        {
            var dto = JsonSerializer.Deserialize<JobShopDto>(json, JsonOptions);
            if (dto == null)
            {
                result.Errors.Add("Failed to parse JSON");
                return result;
            }

            // Store metadata
            result.Name = dto.Name ?? "Imported Job Shop";
            result.Description = dto.Description ?? "";
            result.Makespan = dto.Makespan;
            result.Status = dto.Status;

            // Detect if this is a solution file (has start times)
            result.IsSolution = DetectSolutionFormat(dto.Data);

            var nodeId = startingNodeId;
            var edgeId = startingEdgeId;

            // Determine machine count
            int machineCount = dto.Machines ?? 0;
            if (machineCount == 0 && dto.Data != null)
            {
                // Infer from data - find max machine ID
                machineCount = dto.Data.SelectMany(job => job).Max(op => op[0]) + 1;
            }

            // Determine job count
            int jobCount = dto.Jobs ?? dto.Data?.Length ?? 0;

            // Default machine names if not provided
            var machineNames = dto.MachineNames ?? Enumerable.Range(0, machineCount)
                .Select(i => $"Machine {i + 1}")
                .ToArray();

            // Ensure we have enough machine names
            while (machineNames.Length < machineCount)
            {
                var expanded = machineNames.ToList();
                expanded.Add($"Machine {expanded.Count + 1}");
                machineNames = expanded.ToArray();
            }

            // Job colors
            var jobColors = GetJobColors(jobCount);

            // Create machine nodes
            var machineNodes = new Node[machineCount];
            for (int m = 0; m < machineCount; m++)
            {
                var machineNode = GanttTimelineService.CreateGanttMachineNode(
                    nodeId++,
                    machineNames[m],
                    m
                );
                machineNodes[m] = machineNode;
                result.Nodes.Add(machineNode);
            }

            // Create job nodes
            var jobNodes = new Node[jobCount];
            for (int j = 0; j < jobCount; j++)
            {
                var jobName = dto.JobNames != null && j < dto.JobNames.Length
                    ? dto.JobNames[j]
                    : $"Job {j + 1}";
                var jobNode = GanttTimelineService.CreateGanttJobNode(
                    nodeId++,
                    jobName,
                    jobColors[j]
                );
                jobNodes[j] = jobNode;
                result.Nodes.Add(jobNode);
            }

            // Create task nodes from data
            if (dto.Data == null || dto.Data.Length == 0)
            {
                result.Errors.Add("No job data found in JSON");
                return result;
            }

            var tasks = new List<Node>[jobCount];
            var timeMultiplier = GetTimeMultiplier(dto.TimeUnit);

            for (int job = 0; job < Math.Min(jobCount, dto.Data.Length); job++)
            {
                tasks[job] = new List<Node>();
                var currentTime = TimeSpan.Zero; // Used for problem format (no start times)
                var jobData = dto.Data[job];

                for (int op = 0; op < jobData.Length; op++)
                {
                    if (jobData[op].Length < 2)
                    {
                        result.Warnings.Add($"Job {job + 1}, Operation {op + 1}: Invalid format, expected [machine, duration] or [machine, duration, start]");
                        continue;
                    }

                    int machine = jobData[op][0];
                    int processingTime = jobData[op][1];

                    // Check for start time (solution format)
                    TimeSpan startTime;
                    if (jobData[op].Length >= 3)
                    {
                        // Solution format: [machine, duration, start_time]
                        startTime = TimeSpan.FromMinutes(jobData[op][2] * timeMultiplier);
                    }
                    else
                    {
                        // Problem format: sequential start times
                        startTime = currentTime;
                    }

                    if (machine < 0 || machine >= machineCount)
                    {
                        result.Warnings.Add($"Job {job + 1}, Operation {op + 1}: Machine {machine} out of range (0-{machineCount - 1})");
                        machine = Math.Clamp(machine, 0, machineCount - 1);
                    }

                    var duration = TimeSpan.FromMinutes(processingTime * timeMultiplier);
                    string taskName = $"J{job + 1}-Op{op + 1}";

                    var task = GanttTimelineService.CreateGanttTaskNode(
                        nodeId++,
                        taskName,
                        startTime,
                        duration,
                        jobNodes[job].Id,
                        machineNodes[machine].Id
                    );

                    // Set task color based on job
                    task.FillColor = jobColors[job];
                    task.StrokeColor = GanttColorService.DarkenColor(jobColors[job]);
                    task.GanttRowIndex = machine;

                    tasks[job].Add(task);
                    result.Nodes.Add(task);

                    // Update currentTime for problem format
                    currentTime = startTime + duration;
                }
            }

            // Create precedence edges within each job (operation sequence)
            for (int job = 0; job < jobCount; job++)
            {
                if (tasks[job] == null) continue;

                for (int op = 0; op < tasks[job].Count - 1; op++)
                {
                    var edge = new Edge
                    {
                        Id = edgeId++,
                        From = tasks[job][op].Id,
                        To = tasks[job][op + 1].Id,
                        StrokeColor = "#0F172A",
                        StrokeWidth = 2
                    };
                    result.Edges.Add(edge);
                }
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Import a solution JSON as a SolutionLayer (overrides only).
    /// Returns layer overrides that can be applied to existing Gantt tasks.
    /// </summary>
    public GanttSolutionImportResult ImportSolutionAsLayer(string json, IEnumerable<Node> existingNodes)
    {
        var result = new GanttSolutionImportResult();

        try
        {
            var dto = JsonSerializer.Deserialize<JobShopDto>(json, JsonOptions);
            if (dto == null)
            {
                result.Errors.Add("Failed to parse JSON");
                return result;
            }

            // Must be a solution file
            if (!DetectSolutionFormat(dto.Data))
            {
                result.Errors.Add("JSON is not a solution file (missing start times). Expected format: [machine, duration, start_time]");
                return result;
            }

            result.LayerName = dto.Name ?? "Imported Solution";
            result.Description = dto.Description ?? "";
            result.Makespan = dto.Makespan;
            result.Status = dto.Status;
            result.SolverType = dto.Status ?? "Imported";

            var timeMultiplier = GetTimeMultiplier(dto.TimeUnit);

            // Get existing Gantt tasks grouped by job
            var existingTasks = existingNodes
                .Where(n => n.IsGanttTask && n.GanttJobId.HasValue)
                .GroupBy(n => n.GanttJobId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.GanttStartTime).ToList());

            // Get job nodes to map job index to job node ID
            var jobNodes = existingNodes
                .Where(n => n.IsGanttJob)
                .OrderBy(n => n.Id)
                .ToList();

            if (dto.Data == null || dto.Data.Length == 0)
            {
                result.Errors.Add("No job data found in JSON");
                return result;
            }

            // Create overrides for each task
            for (int jobIndex = 0; jobIndex < dto.Data.Length; jobIndex++)
            {
                // Find the job node ID for this job index
                if (jobIndex >= jobNodes.Count)
                {
                    result.Warnings.Add($"Job {jobIndex + 1}: No matching job node found");
                    continue;
                }

                var jobNodeId = jobNodes[jobIndex].Id;
                if (!existingTasks.TryGetValue(jobNodeId, out var jobTasks))
                {
                    result.Warnings.Add($"Job {jobIndex + 1}: No tasks found for job node {jobNodeId}");
                    continue;
                }

                var jobData = dto.Data[jobIndex];

                for (int opIndex = 0; opIndex < jobData.Length; opIndex++)
                {
                    if (opIndex >= jobTasks.Count)
                    {
                        result.Warnings.Add($"Job {jobIndex + 1}, Op {opIndex + 1}: No matching task found");
                        continue;
                    }

                    if (jobData[opIndex].Length < 3)
                    {
                        result.Warnings.Add($"Job {jobIndex + 1}, Op {opIndex + 1}: Missing start time");
                        continue;
                    }

                    var taskNode = jobTasks[opIndex];
                    int machine = jobData[opIndex][0];
                    int duration = jobData[opIndex][1];
                    int startTime = jobData[opIndex][2];

                    var newStartTime = TimeSpan.FromMinutes(startTime * timeMultiplier);
                    var newDuration = TimeSpan.FromMinutes(duration * timeMultiplier);

                    // Create override only if different from base
                    var nodeOverride = new NodeOverride { NodeId = taskNode.Id };
                    bool hasChanges = false;

                    if (taskNode.GanttStartTime != newStartTime)
                    {
                        nodeOverride.GanttStartTime = newStartTime;
                        hasChanges = true;
                    }

                    if (taskNode.GanttDuration != newDuration)
                    {
                        nodeOverride.GanttDuration = newDuration;
                        hasChanges = true;
                    }

                    if (taskNode.GanttRowIndex != machine)
                    {
                        nodeOverride.GanttRowIndex = machine;
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        result.NodeOverrides[taskNode.Id] = nodeOverride;
                    }
                }
            }

            // Calculate metrics
            if (dto.Makespan.HasValue)
            {
                result.ComputedMetrics["Makespan"] = dto.Makespan.Value;
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Detects if the data array contains start times (solution format).
    /// </summary>
    private static bool DetectSolutionFormat(int[][][]? data)
    {
        if (data == null || data.Length == 0) return false;

        // Check if any operation has 3+ elements (machine, duration, start_time)
        foreach (var job in data)
        {
            foreach (var op in job)
            {
                if (op.Length >= 3) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets a time multiplier based on the time unit specified in the JSON.
    /// </summary>
    private static double GetTimeMultiplier(string? timeUnit)
    {
        if (string.IsNullOrEmpty(timeUnit)) return 1.0; // Default to minutes

        return timeUnit.ToLowerInvariant() switch
        {
            "seconds" or "sec" or "s" => 1.0 / 60.0,  // Convert seconds to minutes
            "minutes" or "min" or "m" => 1.0,          // Minutes as-is
            "hours" or "hour" or "h" => 60.0,          // Convert hours to minutes
            "days" or "day" or "d" => 60.0 * 8,        // 8-hour workday
            _ => 1.0
        };
    }

    /// <summary>
    /// Gets a list of distinct colors for jobs.
    /// </summary>
    private static string[] GetJobColors(int count)
    {
        var palette = GanttColorService.GetExtendedPalette();
        var colors = new string[count];

        for (int i = 0; i < count; i++)
        {
            colors[i] = palette[i % palette.Count].Fill;
        }

        return colors;
    }

    #endregion

    #region JSON Export

    /// <summary>
    /// Export Gantt job-shop scheduling data to JSON format.
    /// Includes start times (solution format) if tasks have scheduled times.
    /// </summary>
    public string ExportToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, string name = "Job Shop Schedule", bool includeSolution = true)
    {
        var nodesList = nodes.ToList();
        var ganttTasks = nodesList.Where(n => n.IsGanttTask).ToList();
        var machineNodes = nodesList.Where(n => n.IsGanttMachine).OrderBy(n => n.GanttRowIndex).ToList();
        var jobNodes = nodesList.Where(n => n.IsGanttJob).ToList();

        if (ganttTasks.Count == 0)
        {
            return JsonSerializer.Serialize(new JobShopDto { Name = name }, JsonOptions);
        }

        // Group tasks by job
        var tasksByJob = ganttTasks
            .Where(t => t.GanttJobId.HasValue)
            .GroupBy(t => t.GanttJobId!.Value)
            .OrderBy(g => g.Key)
            .ToList();

        var data = new List<int[][]>();
        var jobNames = new List<string>();

        // Calculate makespan
        var maxEndTime = ganttTasks
            .Where(t => t.GanttStartTime.HasValue && t.GanttDuration.HasValue)
            .Select(t => t.GanttStartTime!.Value + t.GanttDuration!.Value)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();

        foreach (var jobGroup in tasksByJob)
        {
            // Find job node to get name
            var jobNode = jobNodes.FirstOrDefault(j => j.Id == jobGroup.Key);
            jobNames.Add(jobNode?.Text ?? $"Job {jobGroup.Key}");

            // Get operations in order (by start time)
            var operations = jobGroup.OrderBy(t => t.GanttStartTime).ToList();
            var jobData = new List<int[]>();

            foreach (var task in operations)
            {
                int machineIndex = task.GanttRowIndex;
                int duration = (int)(task.GanttDuration?.TotalMinutes ?? 0);
                int startTime = (int)(task.GanttStartTime?.TotalMinutes ?? 0);

                if (includeSolution)
                {
                    // Solution format with start times
                    jobData.Add(new[] { machineIndex, duration, startTime });
                }
                else
                {
                    // Problem format without start times
                    jobData.Add(new[] { machineIndex, duration });
                }
            }

            data.Add(jobData.ToArray());
        }

        var dto = new JobShopDto
        {
            Name = name,
            Description = includeSolution ? $"Solution exported from DFD Editor" : $"Problem exported from DFD Editor",
            Machines = machineNodes.Count,
            Jobs = data.Count,
            TimeUnit = "minutes",
            Makespan = includeSolution ? (int)maxEndTime.TotalMinutes : null,
            Status = includeSolution ? "EXPORTED" : null,
            MachineNames = machineNodes.Select(m => m.Text ?? $"Machine {m.GanttRowIndex}").ToArray(),
            JobNames = jobNames.ToArray(),
            Data = data.ToArray()
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    #endregion

    #region Format Detection

    /// <summary>
    /// Attempts to detect if the input is valid job-shop JSON.
    /// </summary>
    public bool IsValidJobShopJson(string data)
    {
        var trimmed = data.TrimStart();
        if (!trimmed.StartsWith("{"))
            return false;

        try
        {
            var dto = JsonSerializer.Deserialize<JobShopDto>(data, JsonOptions);
            return dto?.Data != null && dto.Data.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the JSON is a solution file (has start times).
    /// </summary>
    public bool IsSolutionJson(string data)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<JobShopDto>(data, JsonOptions);
            return DetectSolutionFormat(dto?.Data);
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

#region Import Result

/// <summary>
/// Result of a Gantt job-shop import operation.
/// </summary>
public class GanttImportResult
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0 && Nodes.Count > 0;

    /// <summary>True if the imported data included start times (solution format)</summary>
    public bool IsSolution { get; set; }

    /// <summary>Makespan if provided in solution file</summary>
    public int? Makespan { get; set; }

    /// <summary>Solution status (e.g., "OPTIMAL", "FEASIBLE")</summary>
    public string? Status { get; set; }
}

/// <summary>
/// Result of importing a solution as a layer (overrides only).
/// </summary>
public class GanttSolutionImportResult
{
    public string LayerName { get; set; } = "";
    public string Description { get; set; } = "";
    public string SolverType { get; set; } = "Imported";
    public int? Makespan { get; set; }
    public string? Status { get; set; }

    /// <summary>Node overrides to apply to the layer</summary>
    public Dictionary<int, NodeOverride> NodeOverrides { get; set; } = new();

    /// <summary>Computed metrics for the layer</summary>
    public Dictionary<string, decimal> ComputedMetrics { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0 && NodeOverrides.Count > 0;
}

#endregion

#region JSON DTO Classes

/// <summary>
/// DTO for job-shop scheduling problem/solution JSON format.
/// Problem format: data contains [machine_id, processing_time] pairs.
/// Solution format: data contains [machine_id, processing_time, start_time] triples.
/// </summary>
internal class JobShopDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("machines")]
    public int? Machines { get; set; }

    [JsonPropertyName("jobs")]
    public int? Jobs { get; set; }

    [JsonPropertyName("time_unit")]
    public string? TimeUnit { get; set; }

    [JsonPropertyName("makespan")]
    public int? Makespan { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("machine_names")]
    public string[]? MachineNames { get; set; }

    [JsonPropertyName("job_names")]
    public string[]? JobNames { get; set; }

    /// <summary>
    /// Job data: array of jobs, each job is array of operations.
    /// Problem format: [machine_id, processing_time]
    /// Solution format: [machine_id, processing_time, start_time]
    /// </summary>
    [JsonPropertyName("data")]
    public int[][][]? Data { get; set; }
}

#endregion
