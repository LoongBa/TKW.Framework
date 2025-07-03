using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.TKWConfig
{
    public class TkwConfig
    {
        private static TkwConfig _Instance;
        private static readonly object LockObject = new();

        public string JsonConfigurationFilename { get; }

        private TkwConfiguration _TkwConfiguration;
        private TkwConfiguration TkwConfiguration => _TkwConfiguration ??= LoadConfigurations();

        public TkwConfig(string jsonFilename)
        {
            if (string.IsNullOrWhiteSpace(jsonFilename))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(jsonFilename));
            if (!File.Exists(jsonFilename))
                throw new ConfigurationErrorException($"指定的 Xml 配置文件不存在 '{jsonFilename}'");
            JsonConfigurationFilename = jsonFilename;
        }

        public static TkwConfig OpenJsonConfig(string jsonFilename)
        {
            var tkwConfig = new TkwConfig(jsonFilename);
            _Instance ??= tkwConfig;
            tkwConfig.RefreshConfigurations();
            return tkwConfig;
        }

        #region Public Methods

        /// <summary>
        /// Refresh Configurations from Web.config
        /// </summary>
        public void RefreshConfigurations()
        {
            _TkwConfiguration = LoadConfigurations();
        }

        #endregion

        #region 委托的方法

        public IReadOnlyList<Constant> GetConstants()
        {
            return TkwConfiguration.GetConstants();
        }

        public ReadOnlyDictionary<string, Constant> GetConstantDictionary()
        {
            return TkwConfiguration.GetConstantDictionary();
        }

        public Constant GetConstant(string constantName)
        {
            return TkwConfiguration.GetConstant(constantName);
        }

        public IReadOnlyList<Enumeration> GetEnumerations()
        {
            return TkwConfiguration.GetEnumerations();
        }

        public ReadOnlyDictionary<string, Enumeration> GetEnumerationDictionary()
        {
            return TkwConfiguration.GetEnumerationDictionary();
        }

        public Enumeration GetEnumeration(string enumName)
        {
            return TkwConfiguration.GetEnumeration(enumName);
        }

        #endregion

        #region Private Helper Methods

        private TkwConfiguration LoadConfigurations()
        {
            return LoadFromJsonFile(JsonConfigurationFilename);
        }

        public void SaveToJsonFile(string jsonFilename)
        {
            File.WriteAllText(jsonFilename, _TkwConfiguration.ToJson());
        }

        private static TkwConfiguration LoadFromJsonFile(string jsonFilename)
        {
            if (string.IsNullOrWhiteSpace(jsonFilename))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(jsonFilename));
            try
            {
                var json = File.ReadAllText(jsonFilename);
                return json.ToObjectFromJson<TkwConfiguration>();
            }
            catch (Exception e)
            {
                throw new ConfigurationErrorException($"读取 Json 配置文件出错：{e.Message}");
            }
        }

        #endregion
    }
}
