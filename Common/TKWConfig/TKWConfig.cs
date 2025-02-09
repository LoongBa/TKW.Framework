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
        private static readonly object LockObject = new object();

        public string JsonConfigurationFilename { get; }

        private TkwConfiguration _TkwConfiguration;
        private TkwConfiguration TkwConfiguration => _TkwConfiguration ?? (_TkwConfiguration = _LoadConfigurations());

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
            if (_Instance == null) _Instance = tkwConfig;
            tkwConfig.RefreshConfigurations();
            return tkwConfig;
        }

        #region Public Methods

        /// <summary>
        /// Refresh Configurations from Web.config
        /// </summary>
        public void RefreshConfigurations()
        {
            _TkwConfiguration = _LoadConfigurations();
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

        /*

                private Dictionary<string, string> _UnHandledExeptions;

                public ReadOnlyDictionary<string, string> UnHandledExeptions
                {
                    get
                    {
                        return _UnHandledExeptions == null
                            ? _LoadUnHandledExceptions()
                            : new ReadOnlyDictionary<string, string>(_UnHandledExeptions);
                    }
                }

                private ReadOnlyDictionary<string, string> _LoadUnHandledExceptions()
                {
                    var lockObject = new object();
                    lock (lockObject)
                    {
                        if (_UnHandledExeptions == null)
                            _UnHandledExeptions = new Dictionary<string, string>();
                        else
                            _UnHandledExeptions.Clear();
                        var applicationSection = _GetApplicationSection();
                        foreach (UnHandledExeptionElement unHandledExeption in applicationSection.UnHandledExeptions)
                            _UnHandledExeptions.Add(unHandledExeption.Name, unHandledExeption.Description);
                    }
                    return new ReadOnlyDictionary<string, string>(_UnHandledExeptions);
                }

                public void ReloadUnHandledExceptions()
                {
                    _LoadUnHandledExceptions();
                }
        */

        #region Private Helper Methods

        private TkwConfiguration _LoadConfigurations()
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
