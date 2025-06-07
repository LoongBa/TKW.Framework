namespace TKWF.ExcelImporter;

public class RowProcessingErrorEventArgs(int rowIndex, string errorMessage) : EventArgs
{
    public int RowIndex { get; } = rowIndex;
    public string ErrorMessage { get; } = errorMessage;
    public bool ContinueProcessing { get; set; } = true;
}