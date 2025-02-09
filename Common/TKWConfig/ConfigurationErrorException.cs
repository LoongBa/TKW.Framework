using System;
using System.Runtime.Serialization;

namespace TKW.Framework.Common.TKWConfig
{
    /// <summary>
    /// Framework 操作配置文件时的异常
    /// </summary>
    public class ConfigurationErrorException : ApplicationException
    {
        public ConfigurationErrorException()
        {
        }

        public ConfigurationErrorException(string message) : base(message)
        {
        }

        public ConfigurationErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }

    }
}