using System;
using System.Text.RegularExpressions;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.DataType
{
    public class MobilePhone
    {
        public const string _DefaultRegexPatternString_ = "^1[34578][0-9]{9}$";
        private static readonly Regex DefaultRulesRegex =
            new(_DefaultRegexPatternString_, RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Regex _Regex;
        private static readonly MobilePhone Empty = new();

        public MobilePhone(string mobilePhoneNumbers = null, string regexPatternString = null)
        {
            Numbers = string.Empty;
            if (!string.IsNullOrWhiteSpace(regexPatternString))
                _Regex = new Regex(regexPatternString, RegexOptions.Compiled | RegexOptions.Singleline);
            if (!string.IsNullOrWhiteSpace(mobilePhoneNumbers))
                Numbers = mobilePhoneNumbers;
        }

        public MobilePhone(MobilePhone mobilePhone)
        {
            Numbers = string.Empty;
            if (mobilePhone == null) throw new ArgumentNullException(nameof(mobilePhone));
            Numbers = mobilePhone.Numbers;
            //TODO: 复制值
            _Regex.CopySamePropertiesValue(mobilePhone._Regex);
        }

        public string Numbers { get; set; }
        public bool ValidateNumbers()
        {
            return _Regex?.IsMatch(Numbers) ?? ValidateNumbers(Numbers);
        }

        public static bool ValidateNumbers(string numbers)
        {
            return DefaultRulesRegex.IsMatch(numbers);
        }

        public override string ToString()
        {
            return Numbers;
        }
    }
}
