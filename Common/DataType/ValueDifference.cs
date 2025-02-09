using System;
using System.Text.Json.Serialization;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.DataType
{
    public class ValueDifference
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public ValueDifference(string propertyName, object valueBefore, object valueAfter)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(propertyName));
            PropertyName = propertyName;
            ValueBefore = valueBefore ?? throw new ArgumentNullException(nameof(valueBefore));
            ValueStringBefore = valueBefore?.ToString() ?? string.Empty;
            ValueAfter = valueAfter ?? throw new ArgumentNullException(nameof(valueAfter));
            ValueStringAfter = valueAfter?.ToString() ?? string.Empty;
        }
        public ValueDifference(ValueDifference copyFrom)
        {
            if (copyFrom == null) throw new ArgumentNullException(nameof(copyFrom));
            PropertyName = copyFrom.PropertyName;
            ValueBefore = copyFrom.ValueBefore;
            ValueStringBefore = copyFrom.ValueStringBefore;
            ValueAfter = copyFrom.ValueAfter;
            ValueStringAfter = copyFrom.ValueStringAfter;
        }

        public bool IsSameValue()
        {
            return ValueBefore == ValueAfter;
        }

        public bool IsSameValueString(StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (ValueStringBefore == null
                && ValueStringAfter == null) return true;
            return string.Equals(ValueStringBefore, ValueStringAfter, stringComparison);
        }

        #region Overrides of Object

        /// <summary>返回表示当前对象的字符串。</summary>
        /// <returns>表示当前对象的字符串。</returns>
        public override string ToString()
        {
            return this.ToJson();
        }

        #endregion

        public string PropertyName { get; set; }
        public string ValueStringBefore { get; set; }
        public string ValueStringAfter { get; set; }
        [JsonIgnore]
        public object ValueBefore { get; set; }
        [JsonIgnore]
        public object ValueAfter { get; set; }
    }
}