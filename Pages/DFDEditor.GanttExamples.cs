using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class containing Gantt (machine scheduling) examples
/// </summary>
public partial class DFDEditor
{
    /// <summary>
    /// Loads a Taillard 15x15 job-shop scheduling benchmark instance.
    /// This represents 15 jobs, each with 15 operations that must be processed
    /// on 15 different machines in a specific order.
    /// Based on Taillard's benchmark instances for JSSP.
    /// </summary>
    private async Task LoadGanttTaillard15x15()
    {
        // Clear existing Gantt data
        nodes.RemoveAll(n => n.TemplateId == "gantt");
        edges.RemoveAll(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.TemplateId == "gantt" || toNode?.TemplateId == "gantt";
        });

        // Taillard ta01 (15x15) processing times (in minutes)
        var processingTimes = new int[15, 15]
        {
            { 94, 66, 10, 53, 26, 15, 65, 82, 10, 27, 93, 92, 96, 70, 83 }, // Job 1
            { 74, 31, 88, 51, 57, 78,  8,  7, 91, 79, 18, 51, 18, 99, 33 }, // Job 2
            {  4, 82, 40, 86, 50, 54, 21,  6, 54, 68, 82, 20, 39, 35, 68 }, // Job 3
            { 73, 23, 30, 30, 53, 94, 58, 93, 32, 91, 30, 56, 27, 92,  9 }, // Job 4
            { 78, 23, 21, 60, 36, 29, 95, 99, 79,  5, 76, 28, 61, 16, 39 }, // Job 5
            { 63, 18, 85, 30, 46, 31, 78, 86, 90, 90, 11, 19, 97, 15, 36 }, // Job 6
            { 37,  9, 33, 55, 18, 24, 89, 38, 77, 20,  8, 76, 63, 15, 72 }, // Job 7
            { 57, 72, 31, 35, 90, 68, 71, 22, 82, 40, 48, 29, 70, 69, 54 }, // Job 8
            { 33, 14, 53, 90, 71, 33, 28, 84, 91, 39, 91, 95, 69, 97, 18 }, // Job 9
            { 69,  9, 10, 28, 99, 64, 43, 85, 36, 25, 96, 48, 65, 67, 14 }, // Job 10
            { 84, 31, 71, 78, 91, 58, 90, 57, 57, 98, 67, 99, 50, 16, 46 }, // Job 11
            { 27, 91, 46, 18, 88, 42, 71,  7, 66, 74, 10,  5, 16, 10, 50 }, // Job 12
            { 73, 23, 62, 49, 10,  4, 98, 94, 27, 25, 62, 38, 22, 74, 64 }, // Job 13
            { 98, 45, 71, 39, 21, 92, 90,  7, 25, 27, 67, 78, 93, 40, 89 }, // Job 14
            { 69, 60, 79, 20, 98, 15, 11, 94, 56, 67, 28, 22, 22, 77, 63 }  // Job 15
        };

        // Machine sequence for each job (0-indexed machines)
        var machineSequence = new int[15, 15]
        {
            {  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14 }, // Job 1
            {  0,  2,  4,  9,  3,  1,  6, 14,  8,  7, 11,  5, 10, 13, 12 }, // Job 2
            {  1,  0,  3,  2,  8,  5,  7,  6, 10, 11,  4, 12,  9, 14, 13 }, // Job 3
            {  1,  0,  2,  3,  4,  6,  5,  7,  9,  8, 10, 11, 12, 14, 13 }, // Job 4
            {  2,  0,  1,  5,  3,  4,  8,  7,  6, 10, 14, 13,  9, 11, 12 }, // Job 5
            {  2,  1,  5,  3,  8,  9,  4,  0,  7, 10, 11, 14, 13, 12,  6 }, // Job 6
            {  1,  0,  3,  2,  6,  5,  9,  8, 11, 10, 13, 12, 14,  4,  7 }, // Job 7
            {  2,  0,  1,  5,  4,  6,  8,  9,  7, 10, 11, 13, 12, 14,  3 }, // Job 8
            {  0,  1,  3,  5,  2,  9,  7, 10,  4,  8,  6, 11, 12, 14, 13 }, // Job 9
            {  1,  0,  2,  6,  8,  9,  3,  4, 10, 14,  5,  7, 11, 12, 13 }, // Job 10
            {  0,  3,  1,  2,  5,  4,  6,  9,  7, 10,  8, 14, 11, 12, 13 }, // Job 11
            {  2,  0,  3,  1,  6,  7,  5,  9,  4,  8, 10, 11, 13, 14, 12 }, // Job 12
            {  0,  1,  2,  4,  3,  5,  7,  6,  8,  9, 10, 14, 11, 13, 12 }, // Job 13
            {  0,  3,  1,  6,  2,  4,  5,  9,  7, 12,  8, 13, 10, 11, 14 }, // Job 14
            {  2,  0,  1,  3,  4,  6,  5,  8, 10,  9, 12, 11,  7, 14, 13 }  // Job 15
        };

        // Machine names
        var machineNames = new string[]
        {
            "CNC Mill", "Lathe", "Drill Press", "Grinder", "Welder",
            "Paint Booth", "Assembly 1", "Assembly 2", "Inspection", "Packaging",
            "Heat Treat", "Plating", "Polish", "Test Bench", "Final QC"
        };

        // Job colors (15 distinct colors)
        var jobColors = new string[]
        {
            "#ef4444", "#f97316", "#f59e0b", "#eab308", "#84cc16",
            "#22c55e", "#10b981", "#14b8a6", "#06b6d4", "#0ea5e9",
            "#3b82f6", "#6366f1", "#8b5cf6", "#a855f7", "#d946ef"
        };

        int nodeId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        int edgeId = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1;

        // Create 15 machine nodes
        var machineNodes = new Node[15];
        for (int m = 0; m < 15; m++)
        {
            var machineNode = GanttTimelineService.CreateGanttMachineNode(nodeId++, machineNames[m], m);
            machineNodes[m] = machineNode;
            nodes.Add(machineNode);
        }

        // Create 15 job nodes
        var jobNodes = new Node[15];
        for (int j = 0; j < 15; j++)
        {
            var jobNode = GanttTimelineService.CreateGanttJobNode(nodeId++, $"Job {j + 1}", jobColors[j]);
            jobNodes[j] = jobNode;
            nodes.Add(jobNode);
        }

        // Create all tasks (15 jobs x 15 operations = 225 tasks)
        var tasks = new Node[15, 15];
        var currentTime = new TimeSpan[15]; // Track current time for each job

        for (int job = 0; job < 15; job++)
        {
            currentTime[job] = TimeSpan.Zero;
            for (int op = 0; op < 15; op++)
            {
                int machine = machineSequence[job, op];
                var duration = TimeSpan.FromMinutes(processingTimes[job, op]);

                string taskName = $"J{job + 1}-Op{op + 1}";

                var task = GanttTimelineService.CreateGanttTaskNode(
                    nodeId++,
                    taskName,
                    currentTime[job],
                    duration,
                    jobNodes[job].Id,
                    machineNodes[machine].Id
                );

                // Set task color based on job
                task.FillColor = jobColors[job];
                task.StrokeColor = GanttColorService.DarkenColor(jobColors[job]);
                task.GanttRowIndex = machine; // Place on the machine's row


                tasks[job, op] = task;
                nodes.Add(task);

                currentTime[job] += duration;
            }
        }

        // Create precedence edges within each job (operation sequence)
        for (int job = 0; job < 15; job++)
        {
            for (int op = 0; op < 14; op++)
            {
                var edge = new Edge
                {
                    Id = edgeId++,
                    From = tasks[job, op].Id,
                    To = tasks[job, op + 1].Id,
                    StrokeColor = "#0F172A",
                    StrokeWidth = 2
                };
                edges.Add(edge);
            }
        }

        // Enter Gantt mode and initialize view
        isGanttMode = true;
        isProjectMode = false;
        selectedTemplateId = "gantt";
        ganttViewMode = "timeline";
        mode = EditorMode.Select; // Start in Select mode for easier task manipulation

        ganttTimeline = new GanttTimelineService();
        var ganttNodes = nodes.Where(n => n.IsGanttTask).ToList();
        if (ganttNodes.Count > 0)
        {
            ganttTimeline.SetViewRangeFromNodes(ganttNodes, TimeSpan.FromMinutes(30));
        }

        // Position all task nodes
        foreach (var node in ganttNodes)
        {
            ganttTimeline.PositionNodeForTimeline(node);
        }

        // Validate precedences
        ValidateGanttPrecedences();

        nextId = nodeId;
        StateHasChanged();

        // Zoom to fit after loading
        await GanttZoomToFit();
    }

    /// <summary>
    /// Loads an 8x5 job-shop scheduling problem with variable routing lengths.
    /// 8 jobs, 5 machines, each job has different number of operations.
    /// Based on JSP_8x5_02 benchmark instance.
    /// </summary>
    private async Task LoadGanttJSP8x5()
    {
        // Clear existing Gantt data
        nodes.RemoveAll(n => n.TemplateId == "gantt");
        edges.RemoveAll(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.TemplateId == "gantt" || toNode?.TemplateId == "gantt";
        });

        // JSP_8x5_02: 8 jobs, 5 machines, variable routing length
        // Each inner array is [machine_id, processing_time] pairs
        var jobData = new (int Machine, int Duration)[][]
        {
            // Job 1: 5 operations
            new[] { (0, 15), (1, 22), (2, 10), (3, 18), (4, 25) },
            // Job 2: 4 operations
            new[] { (1, 12), (0, 20), (3, 14), (2, 23) },
            // Job 3: 5 operations
            new[] { (2, 19), (4, 13), (0, 21), (1, 16), (3, 24) },
            // Job 4: 4 operations
            new[] { (3, 10), (2, 25), (4, 17), (0, 20) },
            // Job 5: 5 operations
            new[] { (4, 22), (1, 14), (0, 18), (3, 11), (2, 15) },
            // Job 6: 3 operations
            new[] { (0, 13), (3, 21), (1, 24) },
            // Job 7: 5 operations
            new[] { (2, 16), (1, 11), (4, 23), (0, 14), (3, 20) },
            // Job 8: 5 operations
            new[] { (1, 25), (4, 12), (2, 17), (3, 22), (0, 13) }
        };

        // Machine names
        var machineNames = new string[]
        {
            "Mill", "Lathe", "Drill", "Grinder", "Assembly"
        };

        // Job colors (8 distinct colors)
        var jobColors = new string[]
        {
            "#ef4444", // Red
            "#3b82f6", // Blue
            "#22c55e", // Green
            "#f59e0b", // Amber
            "#8b5cf6", // Purple
            "#ec4899", // Pink
            "#06b6d4", // Cyan
            "#f97316"  // Orange
        };

        int nodeId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        int edgeId = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1;

        // Create 5 machine nodes
        var machineNodes = new Node[5];
        for (int m = 0; m < 5; m++)
        {
            var machineNode = GanttTimelineService.CreateGanttMachineNode(nodeId++, machineNames[m], m);
            machineNodes[m] = machineNode;
            nodes.Add(machineNode);
        }

        // Create 8 job nodes
        var jobNodes = new Node[8];
        for (int j = 0; j < 8; j++)
        {
            var jobNode = GanttTimelineService.CreateGanttJobNode(nodeId++, $"Job {j + 1}", jobColors[j]);
            jobNodes[j] = jobNode;
            nodes.Add(jobNode);
        }

        // Create all tasks (variable per job)
        var tasks = new List<Node>[8];
        for (int job = 0; job < 8; job++)
        {
            tasks[job] = new List<Node>();
            var currentTime = TimeSpan.Zero;

            for (int op = 0; op < jobData[job].Length; op++)
            {
                var (machine, processingTime) = jobData[job][op];
                var duration = TimeSpan.FromMinutes(processingTime);

                string taskName = $"J{job + 1}-Op{op + 1}";

                var task = GanttTimelineService.CreateGanttTaskNode(
                    nodeId++,
                    taskName,
                    currentTime,
                    duration,
                    jobNodes[job].Id,
                    machineNodes[machine].Id
                );

                // Set task color based on job
                task.FillColor = jobColors[job];
                task.StrokeColor = GanttColorService.DarkenColor(jobColors[job]);
                task.GanttRowIndex = machine; // Place on the machine's row

                tasks[job].Add(task);
                nodes.Add(task);

                currentTime += duration;
            }
        }

        // Create precedence edges within each job (operation sequence)
        for (int job = 0; job < 8; job++)
        {
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
                edges.Add(edge);
            }
        }

        // Enter Gantt mode and initialize view
        isGanttMode = true;
        isProjectMode = false;
        selectedTemplateId = "gantt";
        ganttViewMode = "timeline";
        mode = EditorMode.Select; // Start in Select mode for easier task manipulation

        ganttTimeline = new GanttTimelineService();
        var ganttNodes = nodes.Where(n => n.IsGanttTask).ToList();
        if (ganttNodes.Count > 0)
        {
            ganttTimeline.SetViewRangeFromNodes(ganttNodes, TimeSpan.FromMinutes(30));
        }

        // Position all task nodes
        foreach (var node in ganttNodes)
        {
            ganttTimeline.PositionNodeForTimeline(node);
        }

        // Validate precedences
        ValidateGanttPrecedences();

        nextId = nodeId;
        StateHasChanged();

        // Zoom to fit after loading
        await GanttZoomToFit();
    }
}
