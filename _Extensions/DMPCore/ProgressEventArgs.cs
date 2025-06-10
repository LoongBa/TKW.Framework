namespace TKWF.DMP.Core;

public class ProgressEventArgs : ProcessEventArgs
{
    public long TotalItems { get; init; }
    public long ProcessedItems { get; init; }
    public long SuccessfulItems { get; init; }
    public long FailedItems { get; init; }
    public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
}