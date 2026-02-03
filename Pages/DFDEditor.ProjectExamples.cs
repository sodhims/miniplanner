using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class containing Project chart example projects and help content (Node-based)
/// </summary>
public partial class DFDEditor
{
    private bool showProjectExamplesMenu = false;
    private bool showProjectHelpDialog = false;

    private static readonly Dictionary<string, (string Name, string Description, Action<DFDEditor> Load)> ProjectExamples = new()
    {
        ["simple"] = ("Simple Project", "Basic 3-task project with dependencies", LoadSimpleProjectChart),
        ["software"] = ("Software Sprint", "2-week software development sprint", LoadSoftwareSprintProject),
        ["construction"] = ("Construction Project", "House building with phases", LoadConstructionProject),
        ["marketing"] = ("Marketing Campaign", "Product launch campaign", LoadMarketingCampaignProject),
        ["help"] = ("Help", "How to use the Project chart", OpenProjectHelp),
    };

    private void LoadProjectExample(string key)
    {
        if (key == "help")
        {
            showProjectHelpDialog = true;
            showProjectExamplesMenu = false;
            StateHasChanged();
            return;
        }

        if (ProjectExamples.TryGetValue(key, out var example))
        {
            example.Load(this);
            showProjectExamplesMenu = false;
            StateHasChanged();
        }
    }

    private static void OpenProjectHelp(DFDEditor editor)
    {
        editor.showProjectHelpDialog = true;
        editor.showProjectExamplesMenu = false;
    }

    /// <summary>
    /// Helper to create a Project task node and add it to the editor
    /// </summary>
    private static Node AddProjectTaskNode(DFDEditor editor, string name, DateTime startDate, int durationDays,
        int? percentComplete = null, string? notes = null, int rowIndex = -1)
    {
        var nodeId = editor.nodes.Count > 0 ? editor.nodes.Max(n => n.Id) + 1 : 1;
        var node = ProjectTimelineService.CreateProjectTaskNode(nodeId, name, startDate, durationDays);
        node.ProjectPercentComplete = percentComplete ?? 0;
        node.ProjectNotes = notes;
        node.ProjectRowIndex = rowIndex >= 0 ? rowIndex : editor.GetprojectNodes().Count();
        editor.nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Helper to create a Project milestone node and add it to the editor
    /// </summary>
    private static Node AddProjectMilestoneNode(DFDEditor editor, string name, DateTime date,
        string? notes = null, int rowIndex = -1)
    {
        var nodeId = editor.nodes.Count > 0 ? editor.nodes.Max(n => n.Id) + 1 : 1;
        var node = ProjectTimelineService.CreateProjectMilestoneNode(nodeId, name, date);
        node.ProjectNotes = notes;
        node.ProjectRowIndex = rowIndex >= 0 ? rowIndex : editor.GetprojectNodes().Count();
        editor.nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Helper to create a Project dependency edge between two nodes
    /// </summary>
    private static void AddProjectDependencyEdge(DFDEditor editor, int fromNodeId, int toNodeId,
        ProjectDependencyType type = ProjectDependencyType.FinishToStart, int lagDays = 0)
    {
        var edgeId = editor.edges.Count > 0 ? editor.edges.Max(e => e.Id) + 1 : 1;
        var edge = new Edge
        {
            Id = edgeId,
            From = fromNodeId,
            To = toNodeId,
            IsProjectDependency = true,
            ProjectDepType = type,
            ProjectLagDays = lagDays,
            StrokeColor = "#64748b",
            Style = EdgeStyle.Ortho
        };
        editor.edges.Add(edge);
    }

    /// <summary>
    /// Clear existing Project data and prepare for new project
    /// </summary>
    private static void ClearProjectData(DFDEditor editor)
    {
        // Remove existing Project nodes
        editor.nodes.RemoveAll(n => n.TemplateId == "project");
        // Remove existing Project dependencies
        editor.edges.RemoveAll(e => e.IsProjectDependency);
    }

    // ============================================
    // SIMPLE PROJECT EXAMPLE
    // ============================================

    private static void LoadSimpleProjectChart(DFDEditor editor)
    {
        ClearProjectData(editor);

        var startDate = new DateTime(2025, 1, 6); // Monday

        // Create tasks
        var planning = AddProjectTaskNode(editor, "Planning", startDate, 3,
            percentComplete: 100, notes: "Define project scope and requirements");

        var development = AddProjectTaskNode(editor, "Development", startDate.AddDays(3), 5,
            percentComplete: 60, notes: "Build the main features");

        var testing = AddProjectTaskNode(editor, "Testing", startDate.AddDays(8), 2,
            notes: "Quality assurance and bug fixes");

        var release = AddProjectMilestoneNode(editor, "Release", startDate.AddDays(10),
            notes: "Project completion");

        // Create dependencies
        AddProjectDependencyEdge(editor, planning.Id, development.Id);
        AddProjectDependencyEdge(editor, development.Id, testing.Id);
        AddProjectDependencyEdge(editor, testing.Id, release.Id);

        // Initialize view
        editor.InitializeProjectViewFromNodes();
    }

    // ============================================
    // SOFTWARE SPRINT EXAMPLE
    // ============================================

    private static void LoadSoftwareSprintProject(DFDEditor editor)
    {
        ClearProjectData(editor);

        var startDate = new DateTime(2025, 1, 6); // Monday - Sprint starts

        // Sprint Planning
        var sprintPlanning = AddProjectTaskNode(editor, "Sprint Planning", startDate, 1,
            percentComplete: 100, notes: "Define sprint goals and backlog");

        // Design Phase
        var uiDesign = AddProjectTaskNode(editor, "UI/UX Design", startDate.AddDays(1), 2,
            percentComplete: 100, notes: "Create wireframes and mockups");

        var apiDesign = AddProjectTaskNode(editor, "API Design", startDate.AddDays(1), 2,
            percentComplete: 100, notes: "Define REST endpoints and data models");

        var designReview = AddProjectMilestoneNode(editor, "Design Review", startDate.AddDays(3),
            notes: "Team review of designs");

        // Development Phase
        var frontendDev = AddProjectTaskNode(editor, "Frontend Development", startDate.AddDays(3), 5,
            percentComplete: 40, notes: "React components and styling");

        var backendDev = AddProjectTaskNode(editor, "Backend Development", startDate.AddDays(3), 4,
            percentComplete: 60, notes: "API implementation and database");

        var integration = AddProjectTaskNode(editor, "Integration", startDate.AddDays(7), 2,
            notes: "Connect frontend to backend");

        // Testing Phase
        var unitTests = AddProjectTaskNode(editor, "Unit Tests", startDate.AddDays(5), 3,
            notes: "Write and run unit tests");

        var e2eTests = AddProjectTaskNode(editor, "E2E Tests", startDate.AddDays(9), 2,
            notes: "End-to-end testing");

        // Deployment
        var codeFreeze = AddProjectMilestoneNode(editor, "Code Freeze", startDate.AddDays(11));
        var deployment = AddProjectTaskNode(editor, "Deployment", startDate.AddDays(11), 1,
            notes: "Deploy to production");

        var sprintReview = AddProjectMilestoneNode(editor, "Sprint Review", startDate.AddDays(12),
            notes: "Demo and retrospective");

        // Dependencies
        AddProjectDependencyEdge(editor, sprintPlanning.Id, uiDesign.Id);
        AddProjectDependencyEdge(editor, sprintPlanning.Id, apiDesign.Id);
        AddProjectDependencyEdge(editor, uiDesign.Id, designReview.Id);
        AddProjectDependencyEdge(editor, apiDesign.Id, designReview.Id);
        AddProjectDependencyEdge(editor, designReview.Id, frontendDev.Id);
        AddProjectDependencyEdge(editor, designReview.Id, backendDev.Id);
        AddProjectDependencyEdge(editor, frontendDev.Id, integration.Id);
        AddProjectDependencyEdge(editor, backendDev.Id, integration.Id);
        AddProjectDependencyEdge(editor, backendDev.Id, unitTests.Id);
        AddProjectDependencyEdge(editor, integration.Id, e2eTests.Id);
        AddProjectDependencyEdge(editor, e2eTests.Id, codeFreeze.Id);
        AddProjectDependencyEdge(editor, codeFreeze.Id, deployment.Id);
        AddProjectDependencyEdge(editor, deployment.Id, sprintReview.Id);

        editor.InitializeProjectViewFromNodes();
    }

    // ============================================
    // CONSTRUCTION PROJECT EXAMPLE
    // ============================================

    private static void LoadConstructionProject(DFDEditor editor)
    {
        ClearProjectData(editor);

        var startDate = new DateTime(2025, 1, 6);

        // Phase 1: Site Preparation
        var permits = AddProjectTaskNode(editor, "Obtain Permits", startDate, 10,
            percentComplete: 100, notes: "Building permits and inspections");

        var sitePrep = AddProjectTaskNode(editor, "Site Preparation", startDate.AddDays(10), 5,
            percentComplete: 100, notes: "Clear land and prepare foundation area");

        var siteMilestone = AddProjectMilestoneNode(editor, "Site Ready", startDate.AddDays(15));

        // Phase 2: Foundation
        var excavation = AddProjectTaskNode(editor, "Excavation", startDate.AddDays(15), 3,
            percentComplete: 100, notes: "Dig foundation");

        var foundation = AddProjectTaskNode(editor, "Pour Foundation", startDate.AddDays(18), 5,
            percentComplete: 80, notes: "Concrete foundation work");

        var foundationCure = AddProjectTaskNode(editor, "Foundation Curing", startDate.AddDays(23), 7,
            notes: "Allow concrete to cure");

        var foundationMilestone = AddProjectMilestoneNode(editor, "Foundation Complete", startDate.AddDays(30));

        // Phase 3: Framing
        var framing = AddProjectTaskNode(editor, "Framing", startDate.AddDays(30), 15,
            notes: "Wood frame structure");

        var roofing = AddProjectTaskNode(editor, "Roofing", startDate.AddDays(40), 7,
            notes: "Install roof structure and shingles");

        var framingMilestone = AddProjectMilestoneNode(editor, "Dried In", startDate.AddDays(47));

        // Phase 4: Systems
        var electrical = AddProjectTaskNode(editor, "Electrical Rough-In", startDate.AddDays(47), 10,
            notes: "Install electrical wiring");

        var plumbing = AddProjectTaskNode(editor, "Plumbing Rough-In", startDate.AddDays(47), 10,
            notes: "Install plumbing pipes");

        var hvac = AddProjectTaskNode(editor, "HVAC Installation", startDate.AddDays(52), 8,
            notes: "Heating and cooling systems");

        var inspection = AddProjectMilestoneNode(editor, "Systems Inspection", startDate.AddDays(60));

        // Phase 5: Finishing
        var insulation = AddProjectTaskNode(editor, "Insulation", startDate.AddDays(60), 5);
        var drywall = AddProjectTaskNode(editor, "Drywall", startDate.AddDays(65), 10);
        var painting = AddProjectTaskNode(editor, "Painting", startDate.AddDays(75), 7);
        var flooring = AddProjectTaskNode(editor, "Flooring", startDate.AddDays(75), 7);
        var fixtures = AddProjectTaskNode(editor, "Fixtures & Trim", startDate.AddDays(82), 10);

        var finalInspection = AddProjectMilestoneNode(editor, "Final Inspection", startDate.AddDays(92));
        var completion = AddProjectMilestoneNode(editor, "Project Complete", startDate.AddDays(95));

        // Dependencies
        AddProjectDependencyEdge(editor, permits.Id, sitePrep.Id);
        AddProjectDependencyEdge(editor, sitePrep.Id, siteMilestone.Id);
        AddProjectDependencyEdge(editor, siteMilestone.Id, excavation.Id);
        AddProjectDependencyEdge(editor, excavation.Id, foundation.Id);
        AddProjectDependencyEdge(editor, foundation.Id, foundationCure.Id);
        AddProjectDependencyEdge(editor, foundationCure.Id, foundationMilestone.Id);
        AddProjectDependencyEdge(editor, foundationMilestone.Id, framing.Id);
        AddProjectDependencyEdge(editor, framing.Id, roofing.Id);
        AddProjectDependencyEdge(editor, roofing.Id, framingMilestone.Id);
        AddProjectDependencyEdge(editor, framingMilestone.Id, electrical.Id);
        AddProjectDependencyEdge(editor, framingMilestone.Id, plumbing.Id);
        AddProjectDependencyEdge(editor, electrical.Id, hvac.Id);
        AddProjectDependencyEdge(editor, plumbing.Id, hvac.Id);
        AddProjectDependencyEdge(editor, hvac.Id, inspection.Id);
        AddProjectDependencyEdge(editor, inspection.Id, insulation.Id);
        AddProjectDependencyEdge(editor, insulation.Id, drywall.Id);
        AddProjectDependencyEdge(editor, drywall.Id, painting.Id);
        AddProjectDependencyEdge(editor, drywall.Id, flooring.Id);
        AddProjectDependencyEdge(editor, painting.Id, fixtures.Id);
        AddProjectDependencyEdge(editor, flooring.Id, fixtures.Id);
        AddProjectDependencyEdge(editor, fixtures.Id, finalInspection.Id);
        AddProjectDependencyEdge(editor, finalInspection.Id, completion.Id);

        editor.InitializeProjectViewFromNodes();
    }

    // ============================================
    // MARKETING CAMPAIGN EXAMPLE
    // ============================================

    private static void LoadMarketingCampaignProject(DFDEditor editor)
    {
        ClearProjectData(editor);

        var startDate = new DateTime(2025, 1, 6);

        // Research Phase
        var marketResearch = AddProjectTaskNode(editor, "Market Research", startDate, 5,
            percentComplete: 100, notes: "Analyze target audience and competitors");

        var brandStrategy = AddProjectTaskNode(editor, "Brand Strategy", startDate.AddDays(3), 4,
            percentComplete: 100, notes: "Define messaging and positioning");

        var strategyApproval = AddProjectMilestoneNode(editor, "Strategy Approved", startDate.AddDays(7));

        // Creative Phase
        var copywriting = AddProjectTaskNode(editor, "Copywriting", startDate.AddDays(7), 5,
            percentComplete: 60, notes: "Write ad copy and landing pages");

        var graphicDesign = AddProjectTaskNode(editor, "Graphic Design", startDate.AddDays(7), 7,
            percentComplete: 40, notes: "Create visual assets");

        var videoProduction = AddProjectTaskNode(editor, "Video Production", startDate.AddDays(10), 10,
            notes: "Produce promotional videos");

        var creativeReview = AddProjectMilestoneNode(editor, "Creative Review", startDate.AddDays(17));

        // Digital Setup
        var websiteUpdates = AddProjectTaskNode(editor, "Website Updates", startDate.AddDays(12), 5,
            notes: "Update landing pages");

        var adCampaignSetup = AddProjectTaskNode(editor, "Ad Campaign Setup", startDate.AddDays(17), 3,
            notes: "Configure Google/Facebook ads");

        var emailSequence = AddProjectTaskNode(editor, "Email Sequence", startDate.AddDays(14), 4,
            notes: "Create drip campaign");

        var socialContent = AddProjectTaskNode(editor, "Social Content Calendar", startDate.AddDays(14), 5,
            notes: "Schedule social media posts");

        var prelaunchCheck = AddProjectMilestoneNode(editor, "Pre-Launch Check", startDate.AddDays(20));

        // Launch Phase
        var softLaunch = AddProjectTaskNode(editor, "Soft Launch", startDate.AddDays(20), 3,
            notes: "Limited release to test audience");

        var launchDay = AddProjectMilestoneNode(editor, "Launch Day", startDate.AddDays(23));

        var fullCampaign = AddProjectTaskNode(editor, "Full Campaign", startDate.AddDays(23), 14,
            notes: "Run all marketing channels");

        var monitoring = AddProjectTaskNode(editor, "Monitor & Optimize", startDate.AddDays(23), 14,
            notes: "Track KPIs and adjust");

        var campaignEnd = AddProjectMilestoneNode(editor, "Campaign End", startDate.AddDays(37));

        // Dependencies
        AddProjectDependencyEdge(editor, marketResearch.Id, brandStrategy.Id);
        AddProjectDependencyEdge(editor, brandStrategy.Id, strategyApproval.Id);
        AddProjectDependencyEdge(editor, strategyApproval.Id, copywriting.Id);
        AddProjectDependencyEdge(editor, strategyApproval.Id, graphicDesign.Id);
        AddProjectDependencyEdge(editor, graphicDesign.Id, videoProduction.Id);
        AddProjectDependencyEdge(editor, copywriting.Id, creativeReview.Id);
        AddProjectDependencyEdge(editor, videoProduction.Id, creativeReview.Id);
        AddProjectDependencyEdge(editor, copywriting.Id, websiteUpdates.Id);
        AddProjectDependencyEdge(editor, creativeReview.Id, adCampaignSetup.Id);
        AddProjectDependencyEdge(editor, copywriting.Id, emailSequence.Id);
        AddProjectDependencyEdge(editor, graphicDesign.Id, socialContent.Id);
        AddProjectDependencyEdge(editor, websiteUpdates.Id, prelaunchCheck.Id);
        AddProjectDependencyEdge(editor, adCampaignSetup.Id, prelaunchCheck.Id);
        AddProjectDependencyEdge(editor, emailSequence.Id, prelaunchCheck.Id);
        AddProjectDependencyEdge(editor, socialContent.Id, prelaunchCheck.Id);
        AddProjectDependencyEdge(editor, prelaunchCheck.Id, softLaunch.Id);
        AddProjectDependencyEdge(editor, softLaunch.Id, launchDay.Id);
        AddProjectDependencyEdge(editor, launchDay.Id, fullCampaign.Id);
        AddProjectDependencyEdge(editor, launchDay.Id, monitoring.Id);
        AddProjectDependencyEdge(editor, fullCampaign.Id, campaignEnd.Id);

        editor.InitializeProjectViewFromNodes();
    }

    // ============================================
    // HELPER METHOD
    // ============================================

    /// <summary>
    /// Initialize Project view from existing nodes (called after loading examples)
    /// </summary>
    private void InitializeProjectViewFromNodes()
    {
        projectTimeline = new ProjectTimelineService();
        ProjectCalendar = new ProjectCalendar();
        projectTimeline.Calendar = ProjectCalendar;

        var projectNodes = GetprojectNodes().ToList();
        projectTimeline.SetViewRangeFromNodes(projectNodes);

        // Position all nodes
        AssignProjectRowIndices(projectNodes);
        foreach (var node in projectNodes)
        {
            projectTimeline.PositionNodeForTimeline(node);
        }

        // Calculate CPM
        var dependencies = GetProjectDependencies().ToList();
        if (dependencies.Count > 0)
        {
            try
            {
                projectCpmResults = CalculateNodeBasedCpm(projectNodes, dependencies);
            }
            catch
            {
                projectCpmResults = null;
            }
        }

        selectedProjectTaskId = null;
        showProjectCriticalPath = false;

        // Update nextId to avoid duplicate IDs when adding new nodes
        if (nodes.Count > 0)
        {
            nextId = nodes.Max(n => n.Id) + 1;
        }

        if (!isProjectMode)
        {
            isProjectMode = true;
        }
    }
}
