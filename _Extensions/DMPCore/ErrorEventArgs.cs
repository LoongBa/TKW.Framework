namespace TKWF.DMP.Core;

public class ErrorEventArgs : ProcessEventArgs
{
    public string Component { get; init; } = string.Empty;
    public Exception Exception { get; init; } = null!;
    public bool SkipAndContinue { get; set; }
}