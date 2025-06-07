using FluentValidation.Results;

namespace TKWF.ExcelImporter;

public class RowValidationErrorEventArgs<T>(int rowIndex, T model, List<ValidationFailure> errors) : EventArgs where T : class
{
    public int RowIndex { get; } = rowIndex;
    public List<ValidationFailure> Errors { get; } = errors;
    public T Model { get; } = model;
    public bool ContinueProcessing { get; set; } = true;
}