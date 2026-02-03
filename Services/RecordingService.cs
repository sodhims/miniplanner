using System.Diagnostics;
using System.Text.Json;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for recording and playing back editor interactions
/// </summary>
public class RecordingService
{
    private Recording? _currentRecording;
    private Stopwatch? _stopwatch;
    private int _sequenceCounter;
    private CancellationTokenSource? _playbackCts;

    /// <summary>
    /// Whether a recording session is currently active
    /// </summary>
    public bool IsRecording => _currentRecording != null && _stopwatch?.IsRunning == true;

    /// <summary>
    /// Whether playback is currently in progress
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Current recording (if any)
    /// </summary>
    public Recording? CurrentRecording => _currentRecording;

    /// <summary>
    /// Event fired when recording state changes
    /// </summary>
    public event Action? OnRecordingStateChanged;

    /// <summary>
    /// Start a new recording session
    /// </summary>
    public void StartRecording(EditorState initialState)
    {
        _currentRecording = new Recording
        {
            InitialState = DeepCopy(initialState)
        };
        _stopwatch = Stopwatch.StartNew();
        _sequenceCounter = 0;

        Console.WriteLine($"Recording started at {DateTime.Now}");
        OnRecordingStateChanged?.Invoke();
    }

    /// <summary>
    /// Stop the current recording session
    /// </summary>
    public Recording? StopRecording()
    {
        if (_currentRecording == null || _stopwatch == null)
            return null;

        _stopwatch.Stop();
        _currentRecording.DurationMs = _stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"Recording stopped. Duration: {_currentRecording.DurationMs}ms, Actions: {_currentRecording.Actions.Count}");

        var recording = _currentRecording;
        _stopwatch = null;

        OnRecordingStateChanged?.Invoke();
        return recording;
    }

    /// <summary>
    /// Record an action during the current recording session
    /// </summary>
    public void RecordAction(RecordedActionType type, string? description, object? data, EditorState? stateAfter = null)
    {
        if (!IsRecording || _currentRecording == null || _stopwatch == null)
            return;

        var action = new RecordedAction
        {
            Sequence = ++_sequenceCounter,
            TimestampMs = _stopwatch.ElapsedMilliseconds,
            Type = type,
            Description = description,
            Data = data != null ? JsonSerializer.SerializeToElement(data) : null,
            StateAfter = stateAfter != null ? DeepCopy(stateAfter) : null
        };

        _currentRecording.Actions.Add(action);
        Console.WriteLine($"Recorded: [{action.Sequence}] {type} - {description} @ {action.TimestampMs}ms");
    }

    /// <summary>
    /// Export the current recording to JSON
    /// </summary>
    public string ExportToJson(Recording? recording = null)
    {
        var rec = recording ?? _currentRecording;
        if (rec == null)
            throw new InvalidOperationException("No recording to export");

        return JsonSerializer.Serialize(rec, RecordingJsonOptions.Default);
    }

    /// <summary>
    /// Import a recording from JSON
    /// </summary>
    public Recording ImportFromJson(string json)
    {
        var recording = JsonSerializer.Deserialize<Recording>(json, RecordingJsonOptions.Default);
        if (recording == null)
            throw new InvalidOperationException("Failed to parse recording JSON");

        _currentRecording = recording;
        OnRecordingStateChanged?.Invoke();
        return recording;
    }

    /// <summary>
    /// Set the current recording (for loaded recordings)
    /// </summary>
    public void SetRecording(Recording recording)
    {
        _currentRecording = recording;
        OnRecordingStateChanged?.Invoke();
    }

    /// <summary>
    /// Clear the current recording
    /// </summary>
    public void ClearRecording()
    {
        _currentRecording = null;
        _stopwatch = null;
        _sequenceCounter = 0;
        OnRecordingStateChanged?.Invoke();
    }

    /// <summary>
    /// Play back a recording with timing (auto-play mode)
    /// </summary>
    public async Task PlaybackAsync(
        Recording recording,
        Action<EditorState, int> onStateChange,
        double speedMultiplier = 1.0,
        CancellationToken cancellationToken = default)
    {
        if (IsPlaying)
            return;

        IsPlaying = true;
        _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Start from initial state
            onStateChange(DeepCopy(recording.InitialState), 0);

            long lastTimestamp = 0;

            for (int i = 0; i < recording.Actions.Count; i++)
            {
                if (_playbackCts.Token.IsCancellationRequested)
                    break;

                var action = recording.Actions[i];

                // Wait for the appropriate time delay
                var delay = (int)((action.TimestampMs - lastTimestamp) / speedMultiplier);
                if (delay > 0)
                {
                    await Task.Delay(delay, _playbackCts.Token);
                }

                // Apply the state change
                if (action.StateAfter != null)
                {
                    onStateChange(DeepCopy(action.StateAfter), i + 1);
                }

                lastTimestamp = action.TimestampMs;
            }
        }
        catch (OperationCanceledException)
        {
            // Playback was cancelled
        }
        finally
        {
            IsPlaying = false;
            _playbackCts = null;
            OnRecordingStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Pause the current playback
    /// </summary>
    public void PausePlayback()
    {
        _playbackCts?.Cancel();
    }

    /// <summary>
    /// Get the state at a specific action index (for step mode)
    /// </summary>
    public EditorState? GetStateAtIndex(Recording recording, int index)
    {
        if (index < 0)
            return DeepCopy(recording.InitialState);

        if (index >= recording.Actions.Count)
            index = recording.Actions.Count - 1;

        // Find the most recent action with a state snapshot
        for (int i = index; i >= 0; i--)
        {
            if (recording.Actions[i].StateAfter != null)
                return DeepCopy(recording.Actions[i].StateAfter!);
        }

        return DeepCopy(recording.InitialState);
    }

    /// <summary>
    /// Step forward one action (for step mode)
    /// </summary>
    public EditorState? StepForward(Recording recording, ref int currentIndex)
    {
        if (currentIndex >= recording.Actions.Count)
            return null;

        currentIndex++;
        return GetStateAtIndex(recording, currentIndex - 1);
    }

    /// <summary>
    /// Step backward one action (for step mode)
    /// </summary>
    public EditorState? StepBackward(Recording recording, ref int currentIndex)
    {
        if (currentIndex <= 0)
        {
            currentIndex = 0;
            return DeepCopy(recording.InitialState);
        }

        currentIndex--;
        return GetStateAtIndex(recording, currentIndex - 1);
    }

    /// <summary>
    /// Jump to start of recording
    /// </summary>
    public EditorState JumpToStart(Recording recording, ref int currentIndex)
    {
        currentIndex = 0;
        return DeepCopy(recording.InitialState);
    }

    /// <summary>
    /// Jump to end of recording
    /// </summary>
    public EditorState? JumpToEnd(Recording recording, ref int currentIndex)
    {
        currentIndex = recording.Actions.Count;
        return GetStateAtIndex(recording, currentIndex - 1);
    }

    private static T DeepCopy<T>(T obj)
    {
        if (obj == null) return default!;
        var json = JsonSerializer.Serialize(obj, RecordingJsonOptions.Default);
        return JsonSerializer.Deserialize<T>(json, RecordingJsonOptions.Default)!;
    }
}
