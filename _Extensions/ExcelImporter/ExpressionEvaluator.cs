namespace TKWF.ExcelImporter;

/// <summary>
/// 表达式计算器实现（基于 System.Linq.Dynamic.Core）
/// </summary>
public class ExpressionEvaluator : IExpressionEvaluator
{
    public bool Evaluate(object instance, string expression)
    {
        try
        {
            // 使用动态表达式计算（简化版，实际项目建议使用 System.Linq.Dynamic.Core）
            if (expression.Contains("=="))
            {
                var parts = expression.Split(["=="], StringSplitOptions.None);
                var left = Calculate(instance, parts[0].Trim());
                var right = Calculate(instance, parts[1].Trim());
                return Equals(left, right);
            }
            else if (expression.Contains('>'))
            {
                var parts = expression.Split('>');
                var left = Convert.ToDecimal(Calculate(instance, parts[0].Trim()));
                var right = Convert.ToDecimal(Calculate(instance, parts[1].Trim()));
                return left > right;
            }
            else if (expression.Contains('<'))
            {
                var parts = expression.Split('<');
                var left = Convert.ToDecimal(Calculate(instance, parts[0].Trim()));
                var right = Convert.ToDecimal(Calculate(instance, parts[1].Trim()));
                return left < right;
            }
            else if (expression.Contains("&&"))
            {
                var parts = expression.Split(["&&"], StringSplitOptions.None);
                return parts.All(p => Evaluate(instance, p.Trim()));
            }
            else if (expression.Contains("||"))
            {
                var parts = expression.Split(["||"], StringSplitOptions.None);
                return parts.Any(p => Evaluate(instance, p.Trim()));
            }

            // 处理简单属性判断
            var value = Calculate(instance, expression);
            if (value is bool b)
                return b;

            return value != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"表达式计算错误: {expression}, 错误: {ex.Message}");
            return false;
        }
    }

    public object? Calculate(object instance, string expression)
    {
        try
        {
            // 计算简单表达式（如属性值、常量）
            if (expression.StartsWith('\'') && expression.EndsWith('\''))
            {
                return expression.Length >= 2 ? expression[1..^1] : string.Empty; // 字符串常量
            }

            if (expression.StartsWith('\"') && expression.EndsWith('\"'))
            {
                return expression.Length >= 2 ? expression[1..^1] : string.Empty; // 字符串常量
            }

            if (decimal.TryParse(expression, out var number))
            {
                return number; // 数值常量
            }

            if (bool.TryParse(expression, out var boolean))
            {
                return boolean; // 布尔常量
            }

            // 尝试获取属性值
            var propertyInfo = instance.GetType().GetProperty(expression);
            if (propertyInfo != null)
            {
                return propertyInfo.GetValue(instance);
            }

            // 支持简单的加法表达式（如 "Prop1+Prop2"）
            if (expression.Contains('+'))
            {
                var parts = expression.Split('+').Select(p => p.Trim()).ToArray();
                var sum = 0m;
                foreach (var part in parts)
                {
                    sum += Convert.ToDecimal(Calculate(instance, part));
                }
                return sum;
            }

            // 支持简单的减法表达式
            if (expression.Contains('-'))
            {
                var parts = expression.Split('-').Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var left = Convert.ToDecimal(Calculate(instance, parts[0]));
                    var right = Convert.ToDecimal(Calculate(instance, parts[1]));
                    return left - right;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"计算属性值错误: {expression}, 错误: {ex.Message}");
            return null;
        }
    }
}