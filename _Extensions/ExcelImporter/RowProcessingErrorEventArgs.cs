namespace TKWF.ExcelImporter;

public class RowProcessingErrorEventArgs(int rowIndex, string errorMessage, Exception? exception) : EventArgs
{
    public int RowIndex { get; } = rowIndex;
    public string ErrorMessage { get; } = errorMessage;
    public Exception? Exception { get; } = exception;
    public bool ContinueProcessing { get; set; } = true;
}