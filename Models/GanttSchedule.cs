namespace dfd2wasm.Models;

/// <summary>
/// Represents a complete Gantt machine scheduling chart.
/// Contains machines, jobs, tasks, and precedence constraints.
/// </summary>
public class GanttSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = "New Schedule";
    public string? Description { get; set; }

    /// <summary>
    /// All machines in the schedule.
    /// </summary>
    public List<GanttMachine> Machines { get; set; } = new();

    /// <summary>
    /// All jobs in the schedule.
    /// </summary>
    public List<GanttJob> Jobs { get; set; } = new();

    /// <summary>
    /// All tasks in the schedule.
    /// </summary>
    public List<GanttTask> Tasks { get; set; } = new();

    /// <summary>
    /// Precedence constraints between tasks.
    /// </summary>
    public List<GanttPrecedence> Precedences { get; set; } = new();

    /// <summary>
    /// Timeline start (typically 0:00).
    /// </summary>
    public TimeSpan TimelineStart { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Timeline end (e.g., 8:00 for an 8-hour shift).
    /// </summary>
    public TimeSpan TimelineEnd { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Auto-incrementing ID counters.
    /// </summary>
    private int _nextTaskId = 1;
    private int _nextJobId = 1;
    private int _nextMachineId = 1;
    private int _nextPrecedenceId = 1;

    /// <summary>
    /// Creates a new task and adds it to the schedule.
    /// </summary>
    public GanttTask AddTask(string name, int jobId, int machineId, TimeSpan startTime, TimeSpan duration)
    {
        var task = new GanttTask
        {
            Id = _nextTaskId++,
            Name = name,
            JobId = jobId,
            MachineId = machineId,
            StartTime = startTime,
            Duration = duration
        };

        Tasks.Add(task);

        // Update job's task list
        var job = Jobs.FirstOrDefault(j => j.Id == jobId);
        job?.AddTask(task.Id);

        // Update machine's task list
        var machine = Machines.FirstOrDefault(m => m.Id == machineId);
        machine?.AddTask(task.Id);

        // Set task row index based on machine
        if (machine != null)
        {
            task.RowIndex = machine.RowIndex;
        }

        return task;
    }

    /// <summary>
    /// Creates a new job and adds it to the schedule.
    /// </summary>
    public GanttJob AddJob(string name, string color)
    {
        var job = new GanttJob
        {
            Id = _nextJobId++,
            Name = name,
            Color = color
        };

        Jobs.Add(job);
        return job;
    }

    /// <summary>
    /// Creates a new machine and adds it to the schedule.
    /// </summary>
    public GanttMachine AddMachine(string name, GanttMachineType type = GanttMachineType.Machine)
    {
        var machine = new GanttMachine
        {
            Id = _nextMachineId++,
            Name = name,
            MachineType = type,
            RowIndex = Machines.Count
        };

        Machines.Add(machine);
        return machine;
    }

    /// <summary>
    /// Creates a precedence constraint between two tasks.
    /// </summary>
    public GanttPrecedence AddPrecedence(int predecessorTaskId, int successorTaskId, TimeSpan? lagTime = null)
    {
        var precedence = new GanttPrecedence
        {
            Id = _nextPrecedenceId++,
            PredecessorTaskId = predecessorTaskId,
            SuccessorTaskId = successorTaskId,
            LagTime = lagTime ?? TimeSpan.Zero
        };

        Precedences.Add(precedence);
        return precedence;
    }

    /// <summary>
    /// Gets a task by ID.
    /// </summary>
    public GanttTask? GetTask(int taskId) => Tasks.FirstOrDefault(t => t.Id == taskId);

    /// <summary>
    /// Gets a job by ID.
    /// </summary>
    public GanttJob? GetJob(int jobId) => Jobs.FirstOrDefault(j => j.Id == jobId);

    /// <summary>
    /// Gets a machine by ID.
    /// </summary>
    public GanttMachine? GetMachine(int machineId) => Machines.FirstOrDefault(m => m.Id == machineId);

    /// <summary>
    /// Gets tasks for a specific job.
    /// </summary>
    public IEnumerable<GanttTask> GetTasksForJob(int jobId) => Tasks.Where(t => t.JobId == jobId);

    /// <summary>
    /// Gets tasks for a specific machine.
    /// </summary>
    public IEnumerable<GanttTask> GetTasksForMachine(int machineId) => Tasks.Where(t => t.MachineId == machineId);

    /// <summary>
    /// Gets predecessors of a task (tasks that must finish before this one starts).
    /// </summary>
    public IEnumerable<GanttTask> GetPredecessors(int taskId)
    {
        var predecessorIds = Precedences
            .Where(p => p.SuccessorTaskId == taskId)
            .Select(p => p.PredecessorTaskId);

        return Tasks.Where(t => predecessorIds.Contains(t.Id));
    }

    /// <summary>
    /// Gets successors of a task (tasks that depend on this one).
    /// </summary>
    public IEnumerable<GanttTask> GetSuccessors(int taskId)
    {
        var successorIds = Precedences
            .Where(p => p.PredecessorTaskId == taskId)
            .Select(p => p.SuccessorTaskId);

        return Tasks.Where(t => successorIds.Contains(t.Id));
    }

    /// <summary>
    /// Removes a task and its associated precedences.
    /// </summary>
    public void RemoveTask(int taskId)
    {
        var task = GetTask(taskId);
        if (task == null) return;

        // Remove from job
        var job = GetJob(task.JobId);
        job?.RemoveTask(taskId);

        // Remove from machine
        var machine = GetMachine(task.MachineId);
        machine?.RemoveTask(taskId);

        // Remove precedences
        Precedences.RemoveAll(p => p.PredecessorTaskId == taskId || p.SuccessorTaskId == taskId);

        // Remove task
        Tasks.Remove(task);
    }

    /// <summary>
    /// Removes a job and all its tasks.
    /// </summary>
    public void RemoveJob(int jobId)
    {
        var job = GetJob(jobId);
        if (job == null) return;

        // Remove all tasks in the job
        var taskIds = job.TaskIds.ToList();
        foreach (var taskId in taskIds)
        {
            RemoveTask(taskId);
        }

        Jobs.Remove(job);
    }

    /// <summary>
    /// Removes a machine and unassigns its tasks.
    /// </summary>
    public void RemoveMachine(int machineId)
    {
        var machine = GetMachine(machineId);
        if (machine == null) return;

        // Unassign tasks from this machine
        foreach (var taskId in machine.TaskIds)
        {
            var task = GetTask(taskId);
            if (task != null)
            {
                task.MachineId = 0; // Unassigned
            }
        }

        Machines.Remove(machine);

        // Recalculate row indices
        for (int i = 0; i < Machines.Count; i++)
        {
            Machines[i].RowIndex = i;
        }
    }

    /// <summary>
    /// Gets the makespan (total schedule length).
    /// </summary>
    public TimeSpan GetMakespan()
    {
        return Tasks.Count > 0 ? Tasks.Max(t => t.EndTime) : TimeSpan.Zero;
    }

    /// <summary>
    /// Gets the number of precedence violations in the current schedule.
    /// </summary>
    public int GetViolationCount()
    {
        return Tasks.Count(t => t.IsViolation);
    }

    /// <summary>
    /// Recalculates the timeline end based on task end times.
    /// </summary>
    public void RecalculateTimelineEnd()
    {
        var makespan = GetMakespan();
        // Add some buffer (round up to next hour)
        var hours = (int)Math.Ceiling(makespan.TotalHours) + 1;
        TimelineEnd = TimeSpan.FromHours(Math.Max(hours, 8));
    }

    /// <summary>
    /// Creates a sample schedule for demonstration.
    /// </summary>
    public static GanttSchedule CreateSample()
    {
        var schedule = new GanttSchedule { Name = "Sample Schedule" };

        // Add machines
        var m1 = schedule.AddMachine("Machine 1");
        var m2 = schedule.AddMachine("Machine 2");
        var m3 = schedule.AddMachine("Machine 3");

        // Add jobs with colors
        var j1 = schedule.AddJob("Job A", "#ef4444"); // Red
        var j2 = schedule.AddJob("Job B", "#3b82f6"); // Blue
        var j3 = schedule.AddJob("Job C", "#22c55e"); // Green

        // Add tasks for Job A (red)
        var t1 = schedule.AddTask("A1", j1.Id, m1.Id, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(45));
        var t2 = schedule.AddTask("A2", j1.Id, m2.Id, TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(30));
        var t3 = schedule.AddTask("A3", j1.Id, m3.Id, TimeSpan.FromMinutes(75), TimeSpan.FromMinutes(60));

        // Add tasks for Job B (blue)
        var t4 = schedule.AddTask("B1", j2.Id, m2.Id, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(60));
        var t5 = schedule.AddTask("B2", j2.Id, m1.Id, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(45));
        var t6 = schedule.AddTask("B3", j2.Id, m3.Id, TimeSpan.FromMinutes(135), TimeSpan.FromMinutes(30));

        // Add tasks for Job C (green)
        var t7 = schedule.AddTask("C1", j3.Id, m3.Id, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(30));
        var t8 = schedule.AddTask("C2", j3.Id, m1.Id, TimeSpan.FromMinutes(105), TimeSpan.FromMinutes(30));

        // Add precedences within jobs
        schedule.AddPrecedence(t1.Id, t2.Id); // A1 -> A2
        schedule.AddPrecedence(t2.Id, t3.Id); // A2 -> A3
        schedule.AddPrecedence(t4.Id, t5.Id); // B1 -> B2
        schedule.AddPrecedence(t5.Id, t6.Id); // B2 -> B3
        schedule.AddPrecedence(t7.Id, t8.Id); // C1 -> C2

        return schedule;
    }
}
