namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 校验与映射模式枚举
/// </summary>
public enum ValidationModeEnum
{
    /// <summary> 属性回填映射 (ApplyToEntity) </summary>
    Mapping = 0,
    /// <summary> DTO 业务校验 (ValidateData) </summary>
    Dto = 1,
    /// <summary> Model 终验 (Validate) </summary>
    Model = 2
}