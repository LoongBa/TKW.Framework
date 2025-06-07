namespace TKWF.ExcelImporter;

/// <summary>
/// 表达式计算器
/// </summary>
public interface IExpressionEvaluator
{
    bool Evaluate(object instance, string expression);
    object? Calculate(object instance, string expression);
}