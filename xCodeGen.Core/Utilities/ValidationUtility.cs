using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Utilities
{
    /// <summary>
    /// 验证规则生成工具
    /// </summary>
    public class ValidationUtility
    {
        /// <summary>
        /// 生成验证规则代码
        /// </summary>
        public string GenerateValidationRules(ParameterMetadata param, string propertyName)
        {
            if (param == null || string.IsNullOrEmpty(propertyName))
                return string.Empty;

            var rules = new System.Text.StringBuilder();

            // 处理必填特性
            if (param.Attributes.Exists(a => 
                a.TypeFullName == "System.ComponentModel.DataAnnotations.RequiredAttribute"))
            {
                rules.AppendLine($"RuleFor(dto => dto.{propertyName}).NotEmpty();");
            }
            // 对于值类型且不可空的参数，添加非默认值验证
            else if (!param.IsNullable && !param.IsCollection && 
                     !TypeUtility.IsStringType(param.TypeFullName) &&
                     TypeUtility.IsNumericType(param.TypeFullName))
            {
                rules.AppendLine($"RuleFor(dto => dto.{propertyName}).NotEqual(default({param.TypeName}));");
            }

            // 处理字符串长度特性
            var maxLengthAttr = param.Attributes.Find(a => 
                a.TypeFullName == "System.ComponentModel.DataAnnotations.MaxLengthAttribute");
            if (maxLengthAttr != null && maxLengthAttr.Properties.TryGetValue("Length", out object lengthObj) &&
                lengthObj is int maxLength)
            {
                rules.AppendLine($"RuleFor(dto => dto.{propertyName}).MaximumLength({maxLength});");
            }

            // 处理数值范围特性
            var rangeAttr = param.Attributes.Find(a => 
                a.TypeFullName == "System.ComponentModel.DataAnnotations.RangeAttribute");
            if (rangeAttr != null && 
                rangeAttr.Properties.TryGetValue("Minimum", out object minObj) &&
                rangeAttr.Properties.TryGetValue("Maximum", out object maxObj))
            {
                rules.AppendLine($"RuleFor(dto => dto.{propertyName}).InclusiveBetween({minObj}, {maxObj});");
            }

            // 处理电子邮件特性
            if (param.Attributes.Exists(a => 
                a.TypeFullName == "System.ComponentModel.DataAnnotations.EmailAddressAttribute"))
            {
                rules.AppendLine($"RuleFor(dto => dto.{propertyName}).EmailAddress();");
            }

            return rules.ToString().TrimEnd();
        }
    }
}
    