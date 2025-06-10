namespace TKWF.DMP.Core;

public abstract class ProcessEventArgs : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string Stage { get; init; } = string.Empty;
}