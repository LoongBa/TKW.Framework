namespace TKWF.DMP.Core;

public class ProcessCompleteEventArgs : ProcessEventArgs
{
    public string ProcessId { get; init; } = string.Empty;
    public Dictionary<string, object> Results { get; init; } = new();
    public TimeSpan Duration { get; init; }
}