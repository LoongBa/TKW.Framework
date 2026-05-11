using System;
using System.Collections.Generic;

namespace TKW.Framework.Domain.Exceptions;

public class DomainException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string ErrorCode { get; set; } = string.Empty;
}

public class DomainInitializationException(string message, Exception? innerException = null)
    : DomainException(message, innerException)
{
    // 默认错误码，方便前端统一处理
    public new string ErrorCode { get; set; } = "DOMAIN_INIT_ERROR";
}

public class SystemSetupRequiredException : DomainInitializationException
{
    public SystemSetupRequiredException(string message = "系统尚未完成业务配置，请前往设置页面。")
        : base(message)
    {
        ErrorCode = "SYSTEM_SETUP_REQUIRED";
    }

    /// <summary>
    /// 缺失的配置项列表，表现层可以直接显示“哪些还没填”
    /// </summary>
    public List<string> MissingConfigs { get; set; } = new();
}

public class InfrastructureInaccessibleException : DomainInitializationException
{
    public InfrastructureInaccessibleException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = "INFRASTRUCTURE_OFFLINE";
    }
}