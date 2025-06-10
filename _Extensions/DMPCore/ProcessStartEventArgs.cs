namespace TKWF.DMP.Core;

public class ProcessStartEventArgs : ProcessEventArgs
{
    public string ProcessId { get; init; } = string.Empty;
    public object Parameters { get; init; } = new();
}