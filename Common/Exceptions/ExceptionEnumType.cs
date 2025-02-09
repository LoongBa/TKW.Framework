using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Exceptions
{
    /// <summary>
    /// 异常所使用的枚举类型
    /// </summary>
    public class ExceptionEnumType
    {
        public ExceptionEnumType(ITKWException e)
        {
            TypeName = e.ErrorType.GetType().FullName;
            var attribute = e.ErrorType?.GetDisplayAttribute();
            ValueString = e.ErrorType.ToString();
            ValueName = attribute?.Name ?? string.Empty;
            ValueDescription = attribute?.Description ?? string.Empty;
            ValuePrompt = attribute?.Prompt ?? string.Empty;
            Value = 0;//e.ErrorType;
        }
        public string ValueString { get; }
        public int Value { get; }
        public string ValueName { get; }
        public string ValueDescription { get; }
        public string ValuePrompt { get; }
        public string TypeName { get; }
    }
}