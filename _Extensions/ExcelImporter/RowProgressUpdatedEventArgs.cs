namespace TKWF.ExcelImporter;

public class RowProgressUpdatedEventArgs(double totalRows, int processedRows, int successRows, int errorRows) : EventArgs
{
    public int SuccessRows { get; } = successRows;
    public int ErrorRows { get; } = errorRows;
    public int ProcessedRows { get; } = processedRows;
    public double TotalRows { get; } = totalRows;
    public bool ContinueProcessing { get; set; } = true;
    public double ProgressPercentage => TotalRows > 0 ? (double)ProcessedRows / TotalRows * 100 : 0;
}