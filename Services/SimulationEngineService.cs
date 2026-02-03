using System.Text.Json;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Represents an entity flowing through the simulation
/// </summary>
public class SimulationEntity
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "Entity";
    public string Color { get; set; } = "#4CAF50";
    public double CreatedAt { get; set; }
    public int SourceNodeId { get; set; }
    public int CurrentNodeId { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>
/// Statistics tracked by a counter
/// </summary>
public class CounterStatistics
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> CountByType { get; set; } = new();
    public List<double> ArrivalTimes { get; set; } = new();
    public List<double> InterArrivalTimes { get; set; } = new();
    public double LastArrivalTime { get; set; }
    public double AverageInterArrival => InterArrivalTimes.Count > 0 ? InterArrivalTimes.Average() : 0;
    public double MinInterArrival => InterArrivalTimes.Count > 0 ? InterArrivalTimes.Min() : 0;
    public double MaxInterArrival => InterArrivalTimes.Count > 0 ? InterArrivalTimes.Max() : 0;
    public double StdDevInterArrival
    {
        get
        {
            if (InterArrivalTimes.Count < 2) return 0;
            var avg = AverageInterArrival;
            var sumSquares = InterArrivalTimes.Sum(t => (t - avg) * (t - avg));
            return Math.Sqrt(sumSquares / (InterArrivalTimes.Count - 1));
        }
    }
    public double Throughput { get; set; } // Entities per time unit
}

/// <summary>
/// Event arguments for simulation state changes
/// </summary>
public class SimulationEventArgs : EventArgs
{
    public double SimulationTime { get; set; }
    public string EventType { get; set; } = "";
    public int? NodeId { get; set; }
    public SimulationEntity? Entity { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// A scheduled event in the simulation
/// </summary>
internal class ScheduledEvent : IComparable<ScheduledEvent>
{
    public double Time { get; set; }
    public int Sequence { get; set; }
    public Action Action { get; set; } = () => { };

    public int CompareTo(ScheduledEvent? other)
    {
        if (other == null) return 1;
        var timeCompare = Time.CompareTo(other.Time);
        return timeCompare != 0 ? timeCompare : Sequence.CompareTo(other.Sequence);
    }
}

/// <summary>
/// The discrete event simulation engine
/// </summary>
public class SimulationEngineService
{
    private readonly SortedSet<ScheduledEvent> _eventQueue = new();
    private CancellationTokenSource? _runCts;
    private readonly Random _random = new();
    private int _eventSequence = 0;

    // Simulation state
    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
    public double ClockTime { get; private set; }
    public double SimulationSpeed { get; set; } = 1.0; // Real-time multiplier

    // Node and edge references
    private List<Node> _nodes = new();
    private List<Edge> _edges = new();
    private Dictionary<int, CounterStatistics> _counterStats = new();
    private Dictionary<int, int> _generatorEntityCounts = new();
    private int _nextEntityId = 1;

    // Events
    public event EventHandler<SimulationEventArgs>? SimulationEvent;
    public event EventHandler<double>? TimeUpdated;
    public event EventHandler? SimulationStarted;
    public event EventHandler? SimulationStopped;
    public event EventHandler? SimulationPaused;
    public event EventHandler? SimulationResumed;

    /// <summary>
    /// Initialize the simulation with nodes and edges
    /// </summary>
    public void Initialize(List<Node> nodes, List<Edge> edges)
    {
        _nodes = nodes.ToList();
        _edges = edges.ToList();
        _counterStats.Clear();
        _generatorEntityCounts.Clear();
        _eventQueue.Clear();
        _nextEntityId = 1;
        _eventSequence = 0;
        ClockTime = 0;

        // Initialize counter statistics for all counter nodes
        foreach (var node in _nodes)
        {
            var simType = SimulationConfigHelper.GetSimulationType(node);
            if (simType == SimulationNodeType.Counter)
            {
                _counterStats[node.Id] = new CounterStatistics();
            }
            if (simType == SimulationNodeType.Generator)
            {
                _generatorEntityCounts[node.Id] = 0;
            }
        }
    }

    /// <summary>
    /// Schedule an event at a specific time
    /// </summary>
    private void ScheduleEvent(double time, Action action)
    {
        _eventQueue.Add(new ScheduledEvent
        {
            Time = time,
            Sequence = _eventSequence++,
            Action = action
        });
    }

    /// <summary>
    /// Start or resume the simulation
    /// </summary>
    public async Task StartAsync()
    {
        if (IsPaused)
        {
            IsPaused = false;
            SimulationResumed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsRunning) return;

        IsRunning = true;
        IsPaused = false;
        _runCts = new CancellationTokenSource();

        // Schedule initial events for generators
        ScheduleGenerators();

        SimulationStarted?.Invoke(this, EventArgs.Empty);

        // Run the simulation loop
        await RunSimulationLoopAsync(_runCts.Token);
    }

    /// <summary>
    /// Pause the simulation
    /// </summary>
    public void Pause()
    {
        IsPaused = true;
        SimulationPaused?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stop the simulation
    /// </summary>
    public void Stop()
    {
        _runCts?.Cancel();
        IsRunning = false;
        IsPaused = false;
        SimulationStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reset the simulation to initial state
    /// </summary>
    public void Reset()
    {
        Stop();
        if (_nodes.Count > 0)
        {
            Initialize(_nodes, _edges);
        }
    }

    /// <summary>
    /// Get statistics for a counter node
    /// </summary>
    public CounterStatistics? GetCounterStats(int nodeId)
    {
        return _counterStats.TryGetValue(nodeId, out var stats) ? stats : null;
    }

    /// <summary>
    /// Get all counter statistics
    /// </summary>
    public Dictionary<int, CounterStatistics> GetAllCounterStats()
    {
        return new Dictionary<int, CounterStatistics>(_counterStats);
    }

    /// <summary>
    /// Schedule generator events
    /// </summary>
    private void ScheduleGenerators()
    {
        foreach (var node in _nodes)
        {
            var simType = SimulationConfigHelper.GetSimulationType(node);
            if (simType == SimulationNodeType.Generator)
            {
                var config = SimulationConfigHelper.GetGeneratorConfig(node);
                if (config != null)
                {
                    var startTime = config.StartTime;
                    ScheduleEvent(startTime, () => GenerateEntity(node.Id));
                }
            }
        }
    }

    /// <summary>
    /// Generate an entity from a generator node
    /// </summary>
    private void GenerateEntity(int generatorNodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == generatorNodeId);
        if (node == null) return;

        var config = SimulationConfigHelper.GetGeneratorConfig(node);
        if (config == null) return;

        // Check termination conditions
        var currentTime = ClockTime;
        if (config.Termination == TerminationCondition.Time && currentTime >= config.StopTime)
            return;
        if (config.Termination == TerminationCondition.CountOrTime && currentTime >= config.StopTime)
            return;

        // Check count termination before generating
        var generatedCount = _generatorEntityCounts.GetValueOrDefault(generatorNodeId, 0);
        if (config.Termination == TerminationCondition.Count ||
            config.Termination == TerminationCondition.CountOrTime)
        {
            if (generatedCount >= config.MaxEntities)
                return;
        }

        // Create entity(ies)
        for (int i = 0; i < config.BatchSize; i++)
        {
            var entity = new SimulationEntity
            {
                Id = _nextEntityId++,
                EntityType = config.EntityType,
                Color = config.EntityColor,
                CreatedAt = currentTime,
                SourceNodeId = generatorNodeId,
                CurrentNodeId = generatorNodeId
            };

            _generatorEntityCounts[generatorNodeId] = generatedCount + 1;
            generatedCount++;

            // Raise event
            RaiseSimulationEvent("EntityCreated", generatorNodeId, entity,
                $"Generated {entity.EntityType} #{entity.Id}");

            // Route entity to connected nodes
            RouteEntity(entity, generatorNodeId);

            // Check if we hit the count limit during batch
            if ((config.Termination == TerminationCondition.Count ||
                 config.Termination == TerminationCondition.CountOrTime) &&
                generatedCount >= config.MaxEntities)
                break;
        }

        // Re-check count after batch
        generatedCount = _generatorEntityCounts.GetValueOrDefault(generatorNodeId, 0);
        if (config.Termination == TerminationCondition.Count ||
            config.Termination == TerminationCondition.CountOrTime)
        {
            if (generatedCount >= config.MaxEntities)
                return;
        }

        // Schedule next generation
        var interArrival = GetRandomValue(config.Distribution, config.Param1, config.Param2, config.Param3);
        if (config.TimingMode == TimingMode.EntitiesPerUnit && interArrival > 0)
        {
            interArrival = 1.0 / interArrival; // Convert rate to interval
        }

        var nextTime = currentTime + Math.Max(0.001, interArrival);

        // Check if next time is before stop time
        if (config.Termination == TerminationCondition.Time ||
            config.Termination == TerminationCondition.CountOrTime)
        {
            if (nextTime > config.StopTime)
                return;
        }

        ScheduleEvent(nextTime, () => GenerateEntity(generatorNodeId));
    }

    /// <summary>
    /// Route an entity from one node to connected nodes
    /// </summary>
    private void RouteEntity(SimulationEntity entity, int fromNodeId)
    {
        var outgoingEdges = _edges.Where(e => e.From == fromNodeId).ToList();
        if (outgoingEdges.Count == 0)
        {
            // No outgoing edges - entity stays here or gets consumed
            return;
        }

        // Check if this is a chance node for probabilistic routing
        var fromNode = _nodes.FirstOrDefault(n => n.Id == fromNodeId);
        if (fromNode != null)
        {
            var simType = SimulationConfigHelper.GetSimulationType(fromNode);
            if (simType == SimulationNodeType.Chance)
            {
                var config = SimulationConfigHelper.GetChanceConfig(fromNode);
                if (config != null && config.Branches.Count > 0 && outgoingEdges.Count >= config.Branches.Count)
                {
                    // Probabilistic routing
                    var roll = _random.NextDouble();
                    double cumulative = 0;
                    for (int i = 0; i < config.Branches.Count; i++)
                    {
                        cumulative += config.Branches[i].Probability;
                        if (roll <= cumulative && i < outgoingEdges.Count)
                        {
                            SendEntityToNode(entity, outgoingEdges[i].To);
                            return;
                        }
                    }
                    // Fallback to last edge
                    SendEntityToNode(entity, outgoingEdges[^1].To);
                    return;
                }
            }
        }

        // Default: send to all connected nodes (broadcast)
        foreach (var edge in outgoingEdges)
        {
            // Clone entity for each branch if multiple edges
            var entityToSend = outgoingEdges.Count > 1
                ? new SimulationEntity
                {
                    Id = _nextEntityId++,
                    EntityType = entity.EntityType,
                    Color = entity.Color,
                    CreatedAt = entity.CreatedAt,
                    SourceNodeId = entity.SourceNodeId,
                    CurrentNodeId = entity.CurrentNodeId,
                    Attributes = new Dictionary<string, object>(entity.Attributes)
                }
                : entity;

            SendEntityToNode(entityToSend, edge.To);
        }
    }

    /// <summary>
    /// Send an entity to a specific node
    /// </summary>
    private void SendEntityToNode(SimulationEntity entity, int toNodeId)
    {
        var toNode = _nodes.FirstOrDefault(n => n.Id == toNodeId);
        if (toNode == null) return;

        entity.CurrentNodeId = toNodeId;
        var simType = SimulationConfigHelper.GetSimulationType(toNode);

        switch (simType)
        {
            case SimulationNodeType.Sink:
                ProcessSink(entity, toNode);
                break;

            case SimulationNodeType.Counter:
                ProcessCounter(entity, toNode);
                RouteEntity(entity, toNodeId); // Continue routing
                break;

            case SimulationNodeType.Chance:
                RouteEntity(entity, toNodeId);
                break;

            case SimulationNodeType.Clock:
                // Clock doesn't process entities directly
                RouteEntity(entity, toNodeId);
                break;

            case SimulationNodeType.Dashboard:
                // Dashboard doesn't process entities
                RouteEntity(entity, toNodeId);
                break;

            default:
                // Generic node - just route through
                RouteEntity(entity, toNodeId);
                break;
        }
    }

    /// <summary>
    /// Process an entity arriving at a sink
    /// </summary>
    private void ProcessSink(SimulationEntity entity, Node sinkNode)
    {
        var config = SimulationConfigHelper.GetSinkConfig(sinkNode);

        RaiseSimulationEvent("EntityConsumed", sinkNode.Id, entity,
            $"{entity.EntityType} #{entity.Id} consumed at {config?.Name ?? "Sink"}");
    }

    /// <summary>
    /// Process an entity passing through a counter
    /// </summary>
    private void ProcessCounter(SimulationEntity entity, Node counterNode)
    {
        if (!_counterStats.TryGetValue(counterNode.Id, out var stats))
        {
            stats = new CounterStatistics();
            _counterStats[counterNode.Id] = stats;
        }

        var currentTime = ClockTime;

        stats.TotalCount++;

        // Track by type
        if (!stats.CountByType.ContainsKey(entity.EntityType))
            stats.CountByType[entity.EntityType] = 0;
        stats.CountByType[entity.EntityType]++;

        // Track inter-arrival times
        if (stats.LastArrivalTime > 0)
        {
            var interArrival = currentTime - stats.LastArrivalTime;
            stats.InterArrivalTimes.Add(interArrival);
        }
        stats.ArrivalTimes.Add(currentTime);
        stats.LastArrivalTime = currentTime;

        // Calculate throughput
        var config = SimulationConfigHelper.GetCounterConfig(counterNode);
        var windowSize = config?.ThroughputWindow ?? 60.0;
        var recentArrivals = stats.ArrivalTimes.Count(t => t >= currentTime - windowSize);
        stats.Throughput = windowSize > 0 ? recentArrivals / windowSize : 0;

        RaiseSimulationEvent("CounterUpdated", counterNode.Id, entity,
            $"Counter: {stats.TotalCount} total");
    }

    /// <summary>
    /// Get a random value from the specified distribution
    /// </summary>
    private double GetRandomValue(DistributionType distribution, double param1, double param2, double param3)
    {
        return distribution switch
        {
            DistributionType.Constant => param1,
            DistributionType.Exponential => ExponentialRandom(param1),
            DistributionType.Uniform => UniformRandom(param1, param2),
            DistributionType.Normal => NormalRandom(param1, param2),
            DistributionType.Triangular => TriangularRandom(param1, param2, param3),
            DistributionType.Erlang => ErlangRandom(param1, (int)param2),
            DistributionType.Poisson => PoissonRandom(param1),
            DistributionType.Binomial => BinomialRandom(param1, (int)param2),
            _ => param1
        };
    }

    private double ExponentialRandom(double mean)
    {
        return -mean * Math.Log(1 - _random.NextDouble());
    }

    private double UniformRandom(double min, double max)
    {
        return min + _random.NextDouble() * (max - min);
    }

    private double NormalRandom(double mean, double stdDev)
    {
        // Box-Muller transform
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + z * stdDev;
    }

    private double TriangularRandom(double min, double mode, double max)
    {
        var u = _random.NextDouble();
        var fc = (mode - min) / (max - min);
        if (u < fc)
            return min + Math.Sqrt(u * (max - min) * (mode - min));
        else
            return max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
    }

    private double ErlangRandom(double mean, int k)
    {
        double sum = 0;
        for (int i = 0; i < k; i++)
            sum += ExponentialRandom(mean / k);
        return sum;
    }

    private double PoissonRandom(double lambda)
    {
        // Returns time until next event in Poisson process
        return ExponentialRandom(1.0 / lambda);
    }

    private double BinomialRandom(double p, int n)
    {
        int successes = 0;
        for (int i = 0; i < n; i++)
            if (_random.NextDouble() < p)
                successes++;
        return successes;
    }

    /// <summary>
    /// Raise a simulation event
    /// </summary>
    private void RaiseSimulationEvent(string eventType, int? nodeId, SimulationEntity? entity, string? message)
    {
        SimulationEvent?.Invoke(this, new SimulationEventArgs
        {
            SimulationTime = ClockTime,
            EventType = eventType,
            NodeId = nodeId,
            Entity = entity,
            Message = message
        });
    }

    /// <summary>
    /// Run the simulation loop
    /// </summary>
    private async Task RunSimulationLoopAsync(CancellationToken ct)
    {
        var lastRealTime = DateTime.UtcNow;
        var lastSimTime = ClockTime;

        while (!ct.IsCancellationRequested && _eventQueue.Count > 0)
        {
            if (IsPaused)
            {
                await Task.Delay(50, ct);
                lastRealTime = DateTime.UtcNow;
                lastSimTime = ClockTime;
                continue;
            }

            // Get and remove next event
            var nextEvent = _eventQueue.Min;
            if (nextEvent == null) break;
            _eventQueue.Remove(nextEvent);

            // Advance clock
            ClockTime = nextEvent.Time;

            // Execute the event action
            nextEvent.Action();

            TimeUpdated?.Invoke(this, ClockTime);

            // Calculate delay for real-time simulation
            var simTimeDelta = ClockTime - lastSimTime;
            var realTimeDelayMs = (int)(simTimeDelta * 1000 / SimulationSpeed);

            // If running faster than real-time, yield occasionally
            if (SimulationSpeed >= 10)
            {
                // Very fast mode - minimal delays
                if ((int)ClockTime % 10 == 0)
                    await Task.Delay(1, ct);
            }
            else if (realTimeDelayMs > 0 && realTimeDelayMs < 100)
            {
                await Task.Delay(Math.Max(1, realTimeDelayMs / 10), ct);
            }
            else if (realTimeDelayMs >= 100)
            {
                await Task.Delay(realTimeDelayMs, ct);
            }

            lastSimTime = ClockTime;
        }

        IsRunning = false;
        SimulationStopped?.Invoke(this, EventArgs.Empty);
    }
}
