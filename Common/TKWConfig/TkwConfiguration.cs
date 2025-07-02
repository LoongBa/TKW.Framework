using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TKW.Framework.Common.TKWConfig
{
    public class TkwConfiguration
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public TkwConfiguration()
        {
            Constants = [];
            Enumerations = [];
        }
        public List<Constant> Constants { get; set; }
        public List<Enumeration> Enumerations { get; set; }


        public IReadOnlyList<Constant> GetConstants()
        {
            var constants = new Constant[Constants.Count];
            Constants.CopyTo(constants, 0);
            return constants;
        }
        public IReadOnlyList<Enumeration> GetEnumerations()
        {
            var enumerations = new Enumeration[Enumerations.Count];
            Enumerations.CopyTo(enumerations, 0);
            return enumerations;
        }

        public ReadOnlyDictionary<string, Constant> GetConstantDictionary()
        {
            var dictionary = Constants.ToDictionary(constant => constant.Name, constant => new Constant(constant));
            return new ReadOnlyDictionary<string, Constant>(dictionary);
        }
        public ReadOnlyDictionary<string, Enumeration> GetEnumerationDictionary()
        {
            var dictionary = Enumerations.ToDictionary(enumeration => enumeration.Name, enumeration => new Enumeration(enumeration));
            return new ReadOnlyDictionary<string, Enumeration>(dictionary);
        }

        public Constant GetConstant(string constantName)
        {
            if (string.IsNullOrWhiteSpace(constantName))
                throw new ArgumentNullException(nameof(constantName));

            var constant = Constants.FirstOrDefault(c => c.Name.Equals(constantName, StringComparison.OrdinalIgnoreCase))
                           ?? throw new ConfigurationErrorException($"尚未定义类型 {constantName} 的常量。");
            return constant;
        }

        public Enumeration GetEnumeration(string enumerationName)
        {
            if (string.IsNullOrWhiteSpace(enumerationName))
                throw new ArgumentNullException(nameof(enumerationName));

            var enumeration = Enumerations.FirstOrDefault(c => c.Name.Equals(enumerationName, StringComparison.OrdinalIgnoreCase))
                              ?? throw new ConfigurationErrorException($"尚未定义类型 {enumerationName} 的枚举。");
            return enumeration;
        }
    }
}