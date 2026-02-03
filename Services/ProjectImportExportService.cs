using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using NodaTime;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for importing and exporting Project chart data in various formats.
/// Works with the Node/Edge model where:
/// - Tasks are Nodes with TemplateId == "project"
/// - Dependencies are Edges with IsProjectDependency == true
/// </summary>
public class ProjectImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions;

    static ProjectImportExportService()
    {
        JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region CSV Column Mappings

    /// <summary>
    /// Known column name variations for common project planning tools
    /// </summary>
    private static readonly Dictionary<string, string[]> ColumnMappings = new()
    {
        // Task ID
        ["Id"] = new[] { "id", "taskid", "task_id", "task id", "uid", "unique id", "wbs", "#", "no", "number" },

        // Task name
        ["Name"] = new[] { "name", "taskname", "task_name", "task name", "title", "task", "description", "activity" },

        // Start date
        ["StartDate"] = new[] { "startdate", "start_date", "start date", "start", "begin", "begin date", "planned start", "actual start" },

        // End date
        ["EndDate"] = new[] { "enddate", "end_date", "end date", "end", "finish", "finish date", "due", "due date", "deadline", "planned finish", "actual finish" },

        // Duration
        ["Duration"] = new[] { "duration", "durationdays", "duration_days", "duration days", "days", "work days", "effort", "work" },

        // Percent complete
        ["PercentComplete"] = new[] { "percentcomplete", "percent_complete", "percent complete", "% complete", "%complete", "progress", "completion", "complete", "done" },

        // Milestone
        ["IsMilestone"] = new[] { "ismilestone", "is_milestone", "milestone", "is milestone", "type" },

        // Summary task (group)
        ["IsSummary"] = new[] { "issummary", "is_summary", "summary", "is summary", "group", "isgroup", "is_group", "is group", "parent task", "summary task" },

        // Parent task
        ["ParentId"] = new[] { "parentid", "parent_id", "parent id", "parent", "parenttaskid", "parent task id", "outline level", "wbs parent" },

        // Resource/Assignee
        ["AssignedTo"] = new[] { "assignedto", "assigned_to", "assigned to", "assignee", "resource", "resources", "resource names", "owner", "responsible" },

        // Priority
        ["Priority"] = new[] { "priority", "importance", "level", "urgency" },

        // Notes
        ["Notes"] = new[] { "notes", "note", "comments", "comment", "description", "details", "remarks" },

        // Dependencies (predecessors)
        ["Predecessors"] = new[] { "predecessors", "predecessor", "depends on", "dependencies", "dependency", "blocked by", "predecessor ids" },

        // Color
        ["Color"] = new[] { "color", "colour", "fill", "fillcolor", "fill color", "bar color" }
    };

    /// <summary>
    /// Maps a header name to the canonical column name
    /// </summary>
    private static string? MapColumnName(string header)
    {
        var normalized = header.Trim().ToLowerInvariant();
        foreach (var (canonical, variations) in ColumnMappings)
        {
            if (variations.Contains(normalized))
                return canonical;
        }
        return null;
    }

    #endregion

    #region CSV Import

    /// <summary>
    /// Import Project nodes from CSV with flexible column mapping.
    /// Automatically detects columns from various project planning tools.
    /// </summary>
    public ProjectImportResult ImportFromCsv(string csv, int startingNodeId = 1, int startingEdgeId = 1)
    {
        var result = new ProjectImportResult();
        var lines = csv.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .ToList();

        if (lines.Count < 2)
        {
            result.Errors.Add("CSV must have at least a header row and one data row");
            return result;
        }

        // Parse header to build column index map
        var headerLine = lines[0];
        var columnMap = BuildColumnMap(headerLine);

        if (!columnMap.ContainsKey("Name"))
        {
            result.Errors.Add("CSV must have a Name/Task column");
            return result;
        }

        // Parse data rows
        var nodeId = startingNodeId;
        var edgeId = startingEdgeId;
        var rowIndex = 0;
        var idMapping = new Dictionary<string, int>(); // Original ID -> New node ID

        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var fields = ParseCsvLine(line);
                var node = CreateNodeFromCsvRow(fields, columnMap, nodeId, rowIndex);

                // Store ID mapping for dependency resolution
                if (columnMap.TryGetValue("Id", out var idIndex) && idIndex < fields.Count)
                {
                    var originalId = fields[idIndex].Trim();
                    if (!string.IsNullOrEmpty(originalId))
                    {
                        idMapping[originalId] = nodeId;
                    }
                }
                idMapping[nodeId.ToString()] = nodeId; // Also map by new ID

                result.Nodes.Add(node);
                nodeId++;
                rowIndex++;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        // Second pass: resolve dependencies
        if (columnMap.TryGetValue("Predecessors", out var predIndex))
        {
            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCsvLine(line);
                if (predIndex >= fields.Count) continue;

                var predecessors = fields[predIndex].Trim();
                if (string.IsNullOrEmpty(predecessors)) continue;

                // Get current node ID
                var currentNodeIndex = i - 1;
                if (currentNodeIndex >= result.Nodes.Count) continue;
                var successorId = result.Nodes[currentNodeIndex].Id;

                // Parse predecessors (comma or semicolon separated, may include type like "3FS+2d")
                var edges = ParsePredecessors(predecessors, successorId, idMapping, edgeId);
                foreach (var edge in edges)
                {
                    result.Edges.Add(edge);
                    edgeId++;
                }
            }
        }

        // Third pass: resolve parent-child relationships (hierarchical grouping)
        if (columnMap.TryGetValue("ParentId", out var parentIdIndex))
        {
            var nodeById = result.Nodes.ToDictionary(n => n.Id);

            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCsvLine(line);
                if (parentIdIndex >= fields.Count) continue;

                var parentIdStr = fields[parentIdIndex].Trim();
                if (string.IsNullOrEmpty(parentIdStr)) continue;

                // Get current node
                var currentNodeIndex = i - 1;
                if (currentNodeIndex >= result.Nodes.Count) continue;
                var childNode = result.Nodes[currentNodeIndex];

                // Resolve parent ID
                if (idMapping.TryGetValue(parentIdStr, out var parentNodeId) && nodeById.TryGetValue(parentNodeId, out var parentNode))
                {
                    // Set parent-child relationship
                    childNode.ParentSuperNodeId = parentNodeId;

                    // Add to parent's contained nodes
                    if (!parentNode.ContainedNodeIds.Contains(childNode.Id))
                    {
                        parentNode.ContainedNodeIds.Add(childNode.Id);
                    }

                    // Mark parent as SuperNode with hierarchical grouping if not already
                    if (!parentNode.IsSuperNode)
                    {
                        parentNode.IsSuperNode = true;
                        parentNode.TemplateShapeId = "summary";
                        parentNode.IsCollapsed = true;
                    }
                    parentNode.GroupType = GroupingType.Hierarchical;
                }
                else
                {
                    result.Warnings.Add($"Row {i + 1}: Parent ID '{parentIdStr}' not found");
                }
            }
        }

        return result;
    }

    private Dictionary<string, int> BuildColumnMap(string headerLine)
    {
        var fields = ParseCsvLine(headerLine);
        var map = new Dictionary<string, int>();

        for (int i = 0; i < fields.Count; i++)
        {
            var canonical = MapColumnName(fields[i]);
            if (canonical != null && !map.ContainsKey(canonical))
            {
                map[canonical] = i;
            }
        }

        return map;
    }

    private Node CreateNodeFromCsvRow(List<string> fields, Dictionary<string, int> columnMap, int nodeId, int rowIndex)
    {
        var node = new Node
        {
            Id = nodeId,
            TemplateId = "project",
            TemplateShapeId = "task",
            ProjectRowIndex = rowIndex
        };

        // Name (required)
        if (columnMap.TryGetValue("Name", out var nameIndex) && nameIndex < fields.Count)
        {
            node.Text = fields[nameIndex].Trim();
            if (string.IsNullOrEmpty(node.Text))
                node.Text = $"Task {nodeId}";
        }

        // Start date
        if (columnMap.TryGetValue("StartDate", out var startIndex) && startIndex < fields.Count)
        {
            node.ProjectStartDate = ParseDate(fields[startIndex]);
        }
        node.ProjectStartDate ??= DateTime.Today;

        // End date
        if (columnMap.TryGetValue("EndDate", out var endIndex) && endIndex < fields.Count)
        {
            node.ProjectEndDate = ParseDate(fields[endIndex]);
        }

        // Duration (if no end date, calculate from duration)
        if (columnMap.TryGetValue("Duration", out var durIndex) && durIndex < fields.Count)
        {
            var duration = ParseDuration(fields[durIndex]);
            if (duration > 0)
            {
                node.ProjectDurationDays = duration;
                if (node.ProjectEndDate == null && node.ProjectStartDate != null)
                {
                    node.ProjectEndDate = node.ProjectStartDate.Value.AddDays(duration - 1);
                }
            }
        }

        // Default end date if not set
        if (node.ProjectEndDate == null)
        {
            node.ProjectEndDate = node.ProjectStartDate;
            node.ProjectDurationDays = 1;
        }

        // Calculate duration if not set
        if (node.ProjectDurationDays == 0 && node.ProjectStartDate != null && node.ProjectEndDate != null)
        {
            node.ProjectDurationDays = (int)(node.ProjectEndDate.Value - node.ProjectStartDate.Value).TotalDays + 1;
        }

        // Percent complete
        if (columnMap.TryGetValue("PercentComplete", out var pctIndex) && pctIndex < fields.Count)
        {
            node.ProjectPercentComplete = ParsePercent(fields[pctIndex]);
        }

        // Milestone
        if (columnMap.TryGetValue("IsMilestone", out var msIndex) && msIndex < fields.Count)
        {
            node.ProjectIsMilestone = ParseMilestone(fields[msIndex]);
            if (node.ProjectIsMilestone)
            {
                node.TemplateShapeId = "milestone";
                node.ProjectEndDate = node.ProjectStartDate;
                node.ProjectDurationDays = 0;
            }
        }

        // Summary/Group task
        if (columnMap.TryGetValue("IsSummary", out var summaryIndex) && summaryIndex < fields.Count)
        {
            var summaryValue = fields[summaryIndex].Trim().ToLowerInvariant();
            if (summaryValue == "yes" || summaryValue == "true" || summaryValue == "1" || summaryValue == "y")
            {
                node.IsSuperNode = true;
                node.TemplateShapeId = "summary";
                node.IsCollapsed = true; // Groups start collapsed
            }
        }

        // Assigned to
        if (columnMap.TryGetValue("AssignedTo", out var assignIndex) && assignIndex < fields.Count)
        {
            node.ProjectAssignedTo = fields[assignIndex].Trim();
        }

        // Priority
        if (columnMap.TryGetValue("Priority", out var prioIndex) && prioIndex < fields.Count)
        {
            if (int.TryParse(fields[prioIndex].Trim(), out var priority))
            {
                node.ProjectPriority = priority;
            }
        }

        // Notes
        if (columnMap.TryGetValue("Notes", out var notesIndex) && notesIndex < fields.Count)
        {
            node.ProjectNotes = fields[notesIndex].Trim();
        }

        // Color
        if (columnMap.TryGetValue("Color", out var colorIndex) && colorIndex < fields.Count)
        {
            var color = fields[colorIndex].Trim();
            if (!string.IsNullOrEmpty(color))
            {
                node.FillColor = color;
            }
        }

        return node;
    }

    private List<Edge> ParsePredecessors(string predecessors, int successorId, Dictionary<string, int> idMapping, int startEdgeId)
    {
        var edges = new List<Edge>();
        var edgeId = startEdgeId;

        // Split by comma, semicolon, or space
        var parts = predecessors.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var (predId, depType, lag) = ParseDependencySpec(part.Trim());

            if (!string.IsNullOrEmpty(predId) && idMapping.TryGetValue(predId, out var predecessorNodeId))
            {
                edges.Add(new Edge
                {
                    Id = edgeId++,
                    From = predecessorNodeId,
                    To = successorId,
                    IsProjectDependency = true,
                    ProjectDepType = depType,
                    ProjectLagDays = lag,
                    Style = EdgeStyle.SmartL
                });
            }
        }

        return edges;
    }

    /// <summary>
    /// Parses dependency specifications like "3", "3FS", "3FS+2d", "3SS-1"
    /// </summary>
    private (string predId, ProjectDependencyType type, int lag) ParseDependencySpec(string spec)
    {
        var type = ProjectDependencyType.FinishToStart;
        var lag = 0;
        var predId = spec;

        // Look for dependency type codes
        var typeMatch = System.Text.RegularExpressions.Regex.Match(spec, @"^(\d+)\s*(FS|SS|FF|SF)?([+-]?\d+)?[dD]?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (typeMatch.Success)
        {
            predId = typeMatch.Groups[1].Value;

            if (typeMatch.Groups[2].Success)
            {
                type = typeMatch.Groups[2].Value.ToUpperInvariant() switch
                {
                    "FS" => ProjectDependencyType.FinishToStart,
                    "SS" => ProjectDependencyType.StartToStart,
                    "FF" => ProjectDependencyType.FinishToFinish,
                    "SF" => ProjectDependencyType.StartToFinish,
                    _ => ProjectDependencyType.FinishToStart
                };
            }

            if (typeMatch.Groups[3].Success && int.TryParse(typeMatch.Groups[3].Value, out var lagValue))
            {
                lag = lagValue;
            }
        }

        return (predId, type, lag);
    }

    #endregion

    #region MS Project XML Import

    /// <summary>
    /// Import from MS Project XML format (.xml export from Microsoft Project)
    /// </summary>
    public ProjectImportResult ImportFromMsProjectXml(string xml, int startingNodeId = 1, int startingEdgeId = 1)
    {
        var result = new ProjectImportResult();

        try
        {
            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var tasks = doc.Descendants(ns + "Task").ToList();
            var nodeId = startingNodeId;
            var edgeId = startingEdgeId;
            var uidToNodeId = new Dictionary<string, int>();
            var rowIndex = 0;

            foreach (var task in tasks)
            {
                var uid = task.Element(ns + "UID")?.Value;
                var name = task.Element(ns + "Name")?.Value;

                // Skip summary task (UID 0) or tasks without names
                if (string.IsNullOrEmpty(uid) || uid == "0" || string.IsNullOrEmpty(name))
                    continue;

                var node = new Node
                {
                    Id = nodeId,
                    Text = name,
                    TemplateId = "project",
                    TemplateShapeId = "task",
                    ProjectRowIndex = rowIndex++
                };

                // Dates
                var startStr = task.Element(ns + "Start")?.Value;
                var finishStr = task.Element(ns + "Finish")?.Value;

                if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var start))
                    node.ProjectStartDate = start.Date;
                else
                    node.ProjectStartDate = DateTime.Today;

                if (!string.IsNullOrEmpty(finishStr) && DateTime.TryParse(finishStr, out var finish))
                    node.ProjectEndDate = finish.Date;
                else
                    node.ProjectEndDate = node.ProjectStartDate;

                // Duration (in hours * 10 in MS Project XML, format like "PT24H0M0S")
                var durationStr = task.Element(ns + "Duration")?.Value;
                if (!string.IsNullOrEmpty(durationStr))
                {
                    node.ProjectDurationDays = ParseMsProjectDuration(durationStr);
                }
                else if (node.ProjectStartDate.HasValue && node.ProjectEndDate.HasValue)
                {
                    node.ProjectDurationDays = (int)(node.ProjectEndDate.Value - node.ProjectStartDate.Value).TotalDays + 1;
                }

                // Milestone
                var milestone = task.Element(ns + "Milestone")?.Value;
                node.ProjectIsMilestone = milestone == "1";
                if (node.ProjectIsMilestone)
                {
                    node.TemplateShapeId = "milestone";
                    node.ProjectDurationDays = 0;
                }

                // Percent complete
                var pctComplete = task.Element(ns + "PercentComplete")?.Value;
                if (int.TryParse(pctComplete, out var pct))
                    node.ProjectPercentComplete = pct;

                // Priority
                var priority = task.Element(ns + "Priority")?.Value;
                if (int.TryParse(priority, out var prio))
                    node.ProjectPriority = prio;

                // Notes
                var notes = task.Element(ns + "Notes")?.Value;
                if (!string.IsNullOrEmpty(notes))
                    node.ProjectNotes = notes;

                // Summary task (has children)
                var summary = task.Element(ns + "Summary")?.Value;
                if (summary == "1")
                {
                    node.TemplateShapeId = "summary";
                    node.IsSuperNode = true;
                }

                uidToNodeId[uid] = nodeId;
                result.Nodes.Add(node);
                nodeId++;
            }

            // Parse dependencies (PredecessorLink elements)
            foreach (var task in tasks)
            {
                var uid = task.Element(ns + "UID")?.Value;
                if (string.IsNullOrEmpty(uid) || !uidToNodeId.TryGetValue(uid, out var successorNodeId))
                    continue;

                var predecessorLinks = task.Elements(ns + "PredecessorLink");
                foreach (var link in predecessorLinks)
                {
                    var predUid = link.Element(ns + "PredecessorUID")?.Value;
                    if (string.IsNullOrEmpty(predUid) || !uidToNodeId.TryGetValue(predUid, out var predecessorNodeId))
                        continue;

                    var typeStr = link.Element(ns + "Type")?.Value ?? "1";
                    var lagStr = link.Element(ns + "LinkLag")?.Value ?? "0";

                    var depType = typeStr switch
                    {
                        "0" => ProjectDependencyType.FinishToFinish,
                        "1" => ProjectDependencyType.FinishToStart,
                        "2" => ProjectDependencyType.StartToFinish,
                        "3" => ProjectDependencyType.StartToStart,
                        _ => ProjectDependencyType.FinishToStart
                    };

                    // LinkLag is in tenths of minutes in MS Project
                    var lag = 0;
                    if (int.TryParse(lagStr, out var lagTenthsMinutes))
                    {
                        lag = lagTenthsMinutes / (10 * 60 * 8); // Convert to days (8-hour workday)
                    }

                    result.Edges.Add(new Edge
                    {
                        Id = edgeId++,
                        From = predecessorNodeId,
                        To = successorNodeId,
                        IsProjectDependency = true,
                        ProjectDepType = depType,
                        ProjectLagDays = lag,
                        Style = EdgeStyle.SmartL
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse MS Project XML: {ex.Message}");
        }

        return result;
    }

    private int ParseMsProjectDuration(string duration)
    {
        // Format is ISO 8601 duration like "PT24H0M0S" or "PT8H0M0S"
        // 8 hours = 1 day
        var match = System.Text.RegularExpressions.Regex.Match(duration, @"PT(\d+)H");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var hours))
        {
            return Math.Max(1, hours / 8);
        }
        return 1;
    }

    #endregion

    #region JSON Import/Export

    /// <summary>
    /// Import Project nodes and edges from JSON format
    /// </summary>
    public ProjectImportResult ImportFromJson(string json, int startingNodeId = 1, int startingEdgeId = 1)
    {
        var result = new ProjectImportResult();

        try
        {
            var dto = JsonSerializer.Deserialize<ProjectChartDto>(json, JsonOptions);
            if (dto == null)
            {
                result.Errors.Add("Failed to parse JSON");
                return result;
            }

            var nodeId = startingNodeId;
            var edgeId = startingEdgeId;
            var oldIdToNewId = new Dictionary<int, int>();
            var rowIndex = 0;

            if (dto.Tasks != null)
            {
                foreach (var taskDto in dto.Tasks)
                {
                    var node = new Node
                    {
                        Id = nodeId,
                        Text = taskDto.Name ?? $"Task {nodeId}",
                        TemplateId = "project",
                        TemplateShapeId = taskDto.IsMilestone ? "milestone" : "task",
                        ProjectStartDate = ParseDate(taskDto.StartDate) ?? DateTime.Today,
                        ProjectEndDate = ParseDate(taskDto.EndDate),
                        ProjectDurationDays = taskDto.DurationDays,
                        ProjectPercentComplete = taskDto.PercentComplete,
                        ProjectIsMilestone = taskDto.IsMilestone,
                        ProjectNotes = taskDto.Notes,
                        ProjectRowIndex = rowIndex++
                    };

                    if (node.ProjectEndDate == null)
                        node.ProjectEndDate = node.ProjectStartDate;

                    oldIdToNewId[taskDto.Id] = nodeId;
                    result.Nodes.Add(node);
                    nodeId++;
                }
            }

            if (dto.Dependencies != null)
            {
                foreach (var depDto in dto.Dependencies)
                {
                    if (!oldIdToNewId.TryGetValue(depDto.PredecessorTaskId, out var fromId) ||
                        !oldIdToNewId.TryGetValue(depDto.SuccessorTaskId, out var toId))
                        continue;

                    var depType = Enum.TryParse<ProjectDependencyType>(depDto.Type, true, out var type)
                        ? type : ProjectDependencyType.FinishToStart;

                    result.Edges.Add(new Edge
                    {
                        Id = edgeId++,
                        From = fromId,
                        To = toId,
                        IsProjectDependency = true,
                        ProjectDepType = depType,
                        ProjectLagDays = depDto.LagDays,
                        Style = EdgeStyle.SmartL
                    });
                }
            }

            // Import resources
            var resourceOldIdToNewId = new Dictionary<int, int>();
            if (dto.Resources != null)
            {
                foreach (var resDto in dto.Resources)
                {
                    var resourceType = ParseResourceType(resDto.Type);
                    var resourceNode = ProjectTimelineService.CreateProjectResourceNode(
                        nodeId,
                        resDto.Name ?? $"Resource {nodeId}",
                        resourceType,
                        resDto.Color
                    );

                    // Set additional resource properties
                    if (!string.IsNullOrEmpty(resDto.Email))
                        resourceNode.ProjectResourceEmail = resDto.Email;
                    if (resDto.Capacity.HasValue)
                        resourceNode.ProjectResourceCapacity = resDto.Capacity.Value;
                    if (resDto.CostPerHour.HasValue)
                        resourceNode.ProjectResourceCostPerHour = resDto.CostPerHour.Value;
                    if (!string.IsNullOrEmpty(resDto.Notes))
                        resourceNode.ProjectNotes = resDto.Notes;

                    resourceOldIdToNewId[resDto.Id] = nodeId;
                    result.Nodes.Add(resourceNode);
                    nodeId++;
                }
            }

            // Import assignments (resource to task mappings)
            if (dto.Assignments != null)
            {
                foreach (var assignDto in dto.Assignments)
                {
                    // Find the task and resource nodes
                    if (!oldIdToNewId.TryGetValue(assignDto.TaskId, out var taskNodeId))
                        continue;
                    if (!resourceOldIdToNewId.TryGetValue(assignDto.ResourceId, out var resourceNodeId))
                        continue;

                    var taskNode = result.Nodes.FirstOrDefault(n => n.Id == taskNodeId);
                    var resourceNode = result.Nodes.FirstOrDefault(n => n.Id == resourceNodeId);

                    if (taskNode == null || resourceNode == null)
                        continue;

                    // Add resource to task's assigned resources list
                    taskNode.ProjectAssignedResourceIds ??= new List<int>();
                    if (!taskNode.ProjectAssignedResourceIds.Contains(resourceNodeId))
                        taskNode.ProjectAssignedResourceIds.Add(resourceNodeId);

                    // Add task to resource's assigned tasks list
                    resourceNode.ProjectAssignedTaskIds ??= new List<int>();
                    if (!resourceNode.ProjectAssignedTaskIds.Contains(taskNodeId))
                        resourceNode.ProjectAssignedTaskIds.Add(taskNodeId);

                    // Store assignment details (quantity/percentage) in a format we can use
                    // For now, we note this in the assignment relationship
                    // TODO: Consider adding assignment metadata storage
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse JSON: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parse resource type string to enum
    /// </summary>
    private static ProjectResourceType ParseResourceType(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return ProjectResourceType.Person;

        return type.ToLowerInvariant() switch
        {
            "person" => ProjectResourceType.Person,
            "team" => ProjectResourceType.Team,
            "equipment" => ProjectResourceType.Equipment,
            "vehicle" => ProjectResourceType.Vehicle,
            "machine" => ProjectResourceType.Machine,
            "tool" => ProjectResourceType.Tool,
            "material" => ProjectResourceType.Material,
            "room" => ProjectResourceType.Room,
            "computer" => ProjectResourceType.Computer,
            "custom" => ProjectResourceType.Custom,
            _ => ProjectResourceType.Person
        };
    }

    /// <summary>
    /// Export Project nodes and edges to JSON format
    /// </summary>
    public string ExportToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, string projectName = "Project Chart")
    {
        var nodesList = nodes.ToList();
        var ProjectTasks = nodesList.Where(n => n.TemplateId == "project" && !n.IsProjectResource).ToList();
        var projectResources = nodesList.Where(n => n.IsProjectResource).ToList();
        var projectEdges = edges.Where(e => e.IsProjectDependency).ToList();

        // Build assignments list from task->resource relationships
        var assignments = new List<ProjectAssignmentDto>();
        foreach (var task in ProjectTasks)
        {
            if (task.ProjectAssignedResourceIds != null)
            {
                foreach (var resourceId in task.ProjectAssignedResourceIds)
                {
                    assignments.Add(new ProjectAssignmentDto
                    {
                        TaskId = task.Id,
                        ResourceId = resourceId,
                        Quantity = 1,
                        Percentage = 100
                    });
                }
            }
        }

        var dto = new ProjectChartDto
        {
            Name = projectName,
            StartDate = ProjectTasks.Any() ? ProjectTasks.Min(n => n.ProjectStartDate)?.ToString("yyyy-MM-dd") : null,
            EndDate = ProjectTasks.Any() ? ProjectTasks.Max(n => n.ProjectEndDate)?.ToString("yyyy-MM-dd") : null,
            Tasks = ProjectTasks.Select(n => new ProjectTaskDto
            {
                Id = n.Id,
                Name = n.Text,
                StartDate = n.ProjectStartDate?.ToString("yyyy-MM-dd"),
                EndDate = n.ProjectEndDate?.ToString("yyyy-MM-dd"),
                DurationDays = n.ProjectDurationDays,
                PercentComplete = n.ProjectPercentComplete,
                IsMilestone = n.ProjectIsMilestone,
                Notes = n.ProjectNotes
            }).ToList(),
            Dependencies = projectEdges.Select(e => new ProjectDependencyDto
            {
                PredecessorTaskId = e.From,
                SuccessorTaskId = e.To,
                Type = e.ProjectDepType.ToString(),
                LagDays = e.ProjectLagDays
            }).ToList(),
            Resources = projectResources.Select(r => new ProjectResourceDto
            {
                Id = r.Id,
                Name = r.Text,
                Type = ResourceTypeToString(r.ProjectResourceType),
                Email = r.ProjectResourceEmail,
                Color = r.FillColor,
                Capacity = r.ProjectResourceCapacity > 0 ? r.ProjectResourceCapacity : null,
                CostPerHour = r.ProjectResourceCostPerHour > 0 ? r.ProjectResourceCostPerHour : null,
                Notes = r.ProjectNotes
            }).ToList(),
            Assignments = assignments.Any() ? assignments : null
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Convert resource type enum to string for JSON export
    /// </summary>
    private static string ResourceTypeToString(ProjectResourceType type)
    {
        return type switch
        {
            ProjectResourceType.Person => "person",
            ProjectResourceType.Team => "team",
            ProjectResourceType.Equipment => "equipment",
            ProjectResourceType.Vehicle => "vehicle",
            ProjectResourceType.Machine => "machine",
            ProjectResourceType.Tool => "tool",
            ProjectResourceType.Material => "material",
            ProjectResourceType.Room => "room",
            ProjectResourceType.Computer => "computer",
            ProjectResourceType.Custom => "custom",
            _ => "person"
        };
    }

    #endregion

    #region CSV Export

    /// <summary>
    /// Export Project nodes to CSV format
    /// </summary>
    public string ExportToCsv(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var projectNodes = nodes.Where(n => n.TemplateId == "project" && !n.IsProjectResource)
            .OrderBy(n => n.ProjectRowIndex)
            .ToList();
        var projectEdges = edges.Where(e => e.IsProjectDependency).ToList();

        // Build predecessor lookup
        var predecessorLookup = projectEdges
            .GroupBy(e => e.To)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Id,Name,Start Date,End Date,Duration,% Complete,Milestone,Summary,Parent Id,Assigned To,Priority,Predecessors,Notes");

        foreach (var node in projectNodes)
        {
            var predecessors = "";
            if (predecessorLookup.TryGetValue(node.Id, out var deps))
            {
                predecessors = string.Join(";", deps.Select(d =>
                {
                    var spec = d.From.ToString();
                    if (d.ProjectDepType != ProjectDependencyType.FinishToStart)
                        spec += d.ProjectDepType.ToString().Substring(0, 2).ToUpperInvariant();
                    if (d.ProjectLagDays != 0)
                        spec += (d.ProjectLagDays > 0 ? "+" : "") + d.ProjectLagDays + "d";
                    return spec;
                }));
            }

            sb.AppendLine(string.Join(",", new[]
            {
                node.Id.ToString(),
                EscapeCsvField(node.Text),
                node.ProjectStartDate?.ToString("yyyy-MM-dd") ?? "",
                node.ProjectEndDate?.ToString("yyyy-MM-dd") ?? "",
                node.ProjectDurationDays.ToString(),
                node.ProjectPercentComplete.ToString(),
                node.ProjectIsMilestone ? "Yes" : "No",
                node.IsSuperNode ? "Yes" : "No",
                node.ParentSuperNodeId?.ToString() ?? "",
                EscapeCsvField(node.ProjectAssignedTo ?? ""),
                node.ProjectPriority.ToString(),
                EscapeCsvField(predecessors),
                EscapeCsvField(node.ProjectNotes ?? "")
            }));
        }

        return sb.ToString();
    }

    #endregion

    #region Format Detection

    /// <summary>
    /// Attempts to detect the format of the input data
    /// </summary>
    public ImportFormat DetectFormat(string data)
    {
        var trimmed = data.TrimStart();

        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            return ImportFormat.Json;

        if (trimmed.StartsWith("<?xml") || trimmed.StartsWith("<Project"))
            return ImportFormat.MsProjectXml;

        // Default to CSV
        return ImportFormat.Csv;
    }

    /// <summary>
    /// Auto-detect format and import
    /// </summary>
    public ProjectImportResult Import(string data, int startingNodeId = 1, int startingEdgeId = 1)
    {
        var format = DetectFormat(data);
        return format switch
        {
            ImportFormat.Json => ImportFromJson(data, startingNodeId, startingEdgeId),
            ImportFormat.MsProjectXml => ImportFromMsProjectXml(data, startingNodeId, startingEdgeId),
            _ => ImportFromCsv(data, startingNodeId, startingEdgeId)
        };
    }

    #endregion

    #region Helper Methods

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Try common date formats
        string[] formats = new[]
        {
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "M/d/yyyy",
            "d/M/yyyy",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "MM-dd-yyyy",
            "d-MMM-yyyy",
            "dd MMM yyyy",
            "MMM d, yyyy",
            "MMMM d, yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr.Trim(), format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // Try general parsing as fallback
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        return null;
    }

    private static int ParseDuration(string durationStr)
    {
        if (string.IsNullOrWhiteSpace(durationStr))
            return 0;

        var cleaned = durationStr.Trim().ToLowerInvariant();

        // Remove common suffixes
        cleaned = cleaned.Replace("days", "").Replace("day", "").Replace("d", "").Trim();

        if (int.TryParse(cleaned, out var days))
            return days;

        // Try parsing as decimal
        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var daysDouble))
            return (int)Math.Ceiling(daysDouble);

        return 0;
    }

    private static int ParsePercent(string percentStr)
    {
        if (string.IsNullOrWhiteSpace(percentStr))
            return 0;

        var cleaned = percentStr.Trim().Replace("%", "").Trim();

        if (int.TryParse(cleaned, out var percent))
            return Math.Clamp(percent, 0, 100);

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var pctDouble))
        {
            // Handle 0-1 range
            if (pctDouble > 0 && pctDouble <= 1)
                pctDouble *= 100;
            return Math.Clamp((int)pctDouble, 0, 100);
        }

        return 0;
    }

    private static bool ParseMilestone(string milestoneStr)
    {
        if (string.IsNullOrWhiteSpace(milestoneStr))
            return false;

        var cleaned = milestoneStr.Trim().ToLowerInvariant();
        return cleaned == "true" || cleaned == "yes" || cleaned == "1" ||
               cleaned == "y" || cleaned == "milestone";
    }

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    #endregion
}

#region Import Result

/// <summary>
/// Result of a Project import operation
/// </summary>
public class ProjectImportResult
{
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0 && Nodes.Count > 0;
}

public enum ImportFormat
{
    Csv,
    Json,
    MsProjectXml
}

#endregion

#region JSON DTO Classes

internal class ProjectChartDto
{
    public string? Name { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public List<ProjectTaskDto>? Tasks { get; set; }
    public List<ProjectDependencyDto>? Dependencies { get; set; }
    public List<ProjectResourceDto>? Resources { get; set; }
    public List<ProjectAssignmentDto>? Assignments { get; set; }
}

internal class ProjectTaskDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int DurationDays { get; set; }
    public int PercentComplete { get; set; }
    public bool IsMilestone { get; set; }
    public int? ParentTaskId { get; set; }
    public string? Notes { get; set; }
}

internal class ProjectDependencyDto
{
    public int PredecessorTaskId { get; set; }
    public int SuccessorTaskId { get; set; }
    public string? Type { get; set; }
    public int LagDays { get; set; }
}

/// <summary>
/// DTO for resource definition in JSON import/export
/// </summary>
internal class ProjectResourceDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    /// <summary>Resource type: person, team, equipment, vehicle, machine, tool, material, room, computer, custom</summary>
    public string? Type { get; set; }
    public string? Email { get; set; }
    public string? Color { get; set; }
    public int? Capacity { get; set; }
    public decimal? CostPerHour { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for task-resource assignment in JSON import/export
/// </summary>
internal class ProjectAssignmentDto
{
    public int TaskId { get; set; }
    public int ResourceId { get; set; }
    /// <summary>Quantity of this resource assigned (default 1)</summary>
    public int Quantity { get; set; } = 1;
    /// <summary>Percentage of resource's time allocated (0-100, default 100)</summary>
    public int Percentage { get; set; } = 100;
}

#endregion
