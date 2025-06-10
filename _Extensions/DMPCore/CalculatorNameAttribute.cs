namespace TKWF.DMP.Core;

[AttributeUsage(AttributeTargets.Class)]
public class CalculatorNameAttribute : Attribute
{
    public string Name { get; }
    public CalculatorNameAttribute(string name) => Name = name;
}