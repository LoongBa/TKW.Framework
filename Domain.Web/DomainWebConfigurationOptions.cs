using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Web;

public class DomainWebConfigurationOptions<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public bool UseSessionUserMiddleware { get; set; } = true;
    public bool UseDomainExceptionMiddleware { get; set; } = true;

    public bool EnableDomainLogging { get; set; }
    public EnumDomainLogLevel LoggingLevel { get; set; } = EnumDomainLogLevel.Minimal;

    public Type? SessionManagerType { get; set; }               // 允许覆盖，带警告
    public Type? ExceptionLoggerFactoryType { get; set; }       // 允许覆盖，带警告

    internal bool AttemptedToOverrideUserHelper { get; set; } = false;
}