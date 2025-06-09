namespace TKWF.DMP.Core.Models;
/// <summary>
/// 验证配置
/// </summary>
public class ValidationConfig
{
    public bool FailOnError { get; set; }
    public List<ValidationRule> Rules { get; set; }
}