using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; // 替换 Autofac
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.TKWConfig;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域用户上下文：管理用户信息、服务解析及会话生命周期
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public class DomainUser<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 支撑 AOP 作用域联动的异步上下文，保存当前作用域的 IServiceProvider
    /// </summary>
    internal static readonly AsyncLocal<IServiceProvider?> _ActiveScope = new();

    [JsonConstructor]
    public DomainUser() { }

    public string SessionKey { get; set; } = string.Empty;
    public bool IsAuthenticated { get; internal set; }

    /// <summary>
    /// 内聚性优化：通过属性接收所属 Host，避免全局静态搜索
    /// </summary>
    [JsonIgnore]
    internal DomainHost<TUserInfo>? AttachedHost { get; set; }

    [JsonIgnore]
    private DomainHost<TUserInfo> Host => AttachedHost
        ?? DomainHost<TUserInfo>.Root
        ?? throw new InvalidOperationException("DomainUser 尚未关联到有效的 DomainHost 实例。");

    /// <summary>
    /// 解析作用域优先级逻辑：
    /// 1. 优先使用 AOP 拦截器创建的子作用域（保证事务与对象一致性）
    /// 2. 其次使用 Host 全局 Provider（作为保底）
    /// </summary>
    [JsonIgnore]
    private IServiceProvider ServiceProvider => _ActiveScope.Value ?? Host.ServiceProvider
        ?? throw new InvalidOperationException("无法获取解析作用域，请确保容器已初始化。");

    [JsonInclude]
    public TUserInfo UserInfo { get; set; } = null!;

    #region 服务解析

    /// <summary>
    /// 解析普通领域服务，并自动注入当前用户上下文
    /// </summary>
    public TDomainService Use<TDomainService>() where TDomainService : IDomainService
    {
        // V4 核心变更：使用 ActivatorUtilities 确保将当前实例 (this) 注入到 Service 的构造函数中
        // 这解决了原生 DI 无法像 Autofac 那样通过 TypedParameter 传参的问题
        return ActivatorUtilities.CreateInstance<TDomainService>(ServiceProvider, this);
    }

    /// <summary>
    /// 解析受 AOP 装饰的合约服务
    /// </summary>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        // 装饰器本身已在 DI 注册，直接解析即可
        return ServiceProvider.GetRequiredService<TAopContract>();
    }

    /// <summary>
    /// 创建日志记录器 
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {
        return Host.LoggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    /// 创建泛型日志记录器
    /// </summary>
    public ILogger<T> CreateLogger<T>()
    {
        return Host.LoggerFactory.CreateLogger<T>();
    }

    #endregion

    #region 角色判断、登录、Claims 转换

    public bool IsInRole<T>(T role) where T : struct, Enum => UserInfo.IsInRole(role);
    public bool IsInRole(string role) => UserInfo.IsInRole(role);

    /// <summary>
    /// 将领域用户信息转换为标准 ClaimsPrincipal
    /// </summary>
    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserInfo.UserIdString),
            new(ClaimTypes.Name, UserInfo.UserName),
            new(ClaimTypes.GivenName, UserInfo.DisplayName),
            new("SessionKey", SessionKey),
            new("LoginFrom", UserInfo.LoginFrom.ToString()),
            new("IsAuthenticated", IsAuthenticated.ToString().ToLowerInvariant()),
        };

        // 将领域角色映射到标准 ClaimTypes.Role，以便 IsInRole 工作
        claims.AddRange(UserInfo.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, "TKW.Domain", ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// 执行登录流程并更新会话
    /// </summary>
    public async Task LoginAsUserAsync(string userName, string passwordHashed, EnumLoginFrom loginFrom)
    {
        var userInfo = await Host.UserHelper.UserLoginAsync(this, userName, passwordHashed, loginFrom);
        UserInfo = userInfo;
        IsAuthenticated = true;
        await UpdateAndActiveSessionAsync();
    }

    /// <summary>
    /// 激活并同步当前会话状态
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var session = await Host.SessionManager!.GetSessionAsync(SessionKey);
        var newSession = session.BindUser(this);
        await Host.SessionManager.UpdateAndActiveSessionAsync(newSession.Key, _ => newSession);
        return newSession;
    }

    #endregion

    #region 配置项访问

    /// <summary>
    /// 尝试从 DomainOptions 中获取配置
    /// </summary>
    public string? TryGetOption(string keyName)
    {
        Host.Options.ConfigDictionary.TryGetValue(keyName, out var value);
        return value;
    }

    /// <summary>
    /// 获取必须存在的配置，不存在则抛出异常
    /// </summary>
    public string GetRequiredOption(string keyName)
    {
        return TryGetOption(keyName) ?? throw new ConfigurationErrorException(keyName);
    }

    #endregion

    public IDomainUnitOfWork GetUow()
    {
        // 从当前作用域解析管理器
        var manager = ServiceProvider.GetService<IDomainUnitOfWorkManager>();
        if (manager == null)
            throw new DomainException("未注册 IDomainUnitOfWorkManager，无法提供 UoW 支持。");

        return manager.GetUnitOfWork();
    }
}