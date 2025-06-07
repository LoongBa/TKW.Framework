namespace TKWF.ExcelImporter;

public class RowProgressUpdatedEventArgs(int currentProgress, int totalRows) : EventArgs
{
    public int CurrentProgress { get; } = currentProgress;
    public int TotalRows { get; } = totalRows;
    public bool ContinueProcessing { get; set; } = true;
    public double ProgressPercentage => TotalRows > 0 ? (double)CurrentProgress / TotalRows * 100 : 0;
}