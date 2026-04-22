using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域服务基类，所有领域服务应继承此类
/// </summary>
public abstract class DomainServiceBase<TUserInfo> : IDomainService
    where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 当前操作的用户上下文（不可为 null）
    /// </summary>
    protected internal DomainUser<TUserInfo> User { get; }

    /// <summary>
    /// 使用其他领域服务（通过 User 的依赖解析）
    /// </summary>
    /// <typeparam name="TService">目标领域服务类型，必须继承 DomainServiceBase</typeparam>
    /// <typeparam name="TUserInfo"></typeparam>
    /// <returns>解析到的服务实例</returns>
    protected TService Use<TService>() where TService : IDomainService
    {
        return User.Use<TService>();
    }

    /// <summary>
    /// 初始化领域服务基类
    /// </summary>
    /// <param name="user">当前用户上下文（不可为 null）</param>
    protected DomainServiceBase(DomainUser<TUserInfo> user)
    {
        User = user.EnsureNotNull(nameof(user));
    }

    /// <summary>
    /// 智能解析搜索字符串（配合代码生成器的自动查询使用）
    /// 规则：默认精确匹配；'*' 开头模糊匹配；'**' 开头转义为精确匹配。
    /// </summary>
    protected (string Value, bool IsFuzzy) ParseSearchValue(string input)
    {
        if (string.IsNullOrEmpty(input)) return (input, false);
        if (input.StartsWith("**")) return (input[1..], false);
        if (input.StartsWith("*")) return (input[1..], true);
        return (input, false);
    }
}

/// <summary>
/// 领域控制器基类：可选，用于封装 DomainService + IAopContract
/// 可作为 AOP 切入点或控制器基类
/// </summary>
public abstract class DomainControllerBase<TUserInfo> : DomainServiceBase<TUserInfo>, IAopContract
    where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 初始化领域控制器
    /// </summary>
    /// <param name="user">当前用户上下文（不可为 null）</param>
    protected DomainControllerBase(DomainUser<TUserInfo> user) : base(user)
    {
        // 可在此处添加控制器特有的初始化逻辑
    }
}