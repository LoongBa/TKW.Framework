using TKW.Framework.Domain;

namespace TKW.Framework.Domain.Web;

/// <summary>
/// Web 专用领域配置扩展
/// </summary>
public class DomainWebConfigurationOptions : DomainOptions
{
    public bool UseDomainExceptionMiddleware { get; set; } = true;
    public bool UseSessionUserMiddleware { get; set; } = true;
    public bool EnableDomainLogging { get; set; } = false;

    public bool SuppressRoutingWarning { get; set; } = false;
    internal bool HasRoutingPhase { get; set; } = false;
}