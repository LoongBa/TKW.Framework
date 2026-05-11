using System.Threading.Tasks;

namespace TKW.Framework.Domain.Interfaces;

public interface IDomainSystemSetup
{
    /// <summary>
    /// 主动触发系统与业务就绪状态的校验
    /// </summary>
    Task<bool> ValidateSystemReadinessAsync();
}