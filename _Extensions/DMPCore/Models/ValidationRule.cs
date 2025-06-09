namespace TKWF.DMPCore.Models;
/// <summary>
/// 验证规则
/// </summary>
public class ValidationRule
{
    public string Field { get; set; }
    public string Type { get; set; }
    public bool Required { get; set; }
}