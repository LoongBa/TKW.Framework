using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Permission;

namespace TKW.Framework.Domain;

public class DomainUser : ClaimsPrincipal
{
    internal DomainUser() { }

    public string UserIdString
    {
        get => FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        set => UpdateClaim(ClaimTypes.NameIdentifier, value);
    }

    public string SessionKey
    {
        get => FindFirst("SessionKey")?.Value ?? string.Empty;
        set => UpdateClaim("SessionKey", value);
    }

    /// <summary>
    /// 获取当前用户的所有角色名称（基于 Claims 自动读取）
    /// </summary>
    public IEnumerable<string> RoleNames =>
        FindAll(ClaimTypes.Role)
            .Select(c => c.Value.Trim()) // 去除首尾空白
            .Where(r => !string.IsNullOrWhiteSpace(r));

    /// <summary>
    /// 获取当前用户所有 Identity 的认证类型（枚举集合，空值转为 Unset）
    /// </summary>
    public IEnumerable<UserAuthenticationType> AuthenticationTypes
    {
        get
        {
            return Identities
                // 读取每个 Identity 原生的 AuthenticationType 属性
                .Select(identity =>
                {
                    // 空/无效值转为 Unset，有效值解析为枚举
                    if (string.IsNullOrWhiteSpace(identity.AuthenticationType) ||
                        !Enum.TryParse<UserAuthenticationType>(identity.AuthenticationType, out var authType))
                    {
                        return UserAuthenticationType.Unset;
                    }
                    return authType;
                })
                // 去重（避免多个 Identity 重复同一认证类型）
                .Distinct();
        }
    }

    /// <summary>
    /// 领域中的用户信息（改为不用泛型，需自行检查、装箱拆箱）
    /// </summary>
    public IUserInfo UserInfo { get; protected internal set; }

    public UserPermissionSet Permissions { get; } = new();

    public Func<DomainHost> DomainHostFactory { get; private set; }

    protected DomainUser(Func<DomainHost> domainHostFactory) : this()
    {
        DomainHostFactory = domainHostFactory.EnsureNotNull(name: nameof(domainHostFactory));
    }

    protected internal Func<DomainHost> SetDomainHostFactory(Func<DomainHost> domainHostFactory)
    {
        return DomainHostFactory = domainHostFactory.EnsureNotNull(name: nameof(domainHostFactory));
    }

    /// <summary>
    /// 使用 DomainService 领域服务
    /// </summary>
    /// <remarks>注意：必须在 DomainHost.Initial() 中注册领域服务</remarks>
    /// <typeparam name="TDomainService">领域服务的接口类型</typeparam>
    public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase
    {
        return DomainHostFactory().EnsureNotNull(name: nameof(DomainHostFactory)).Container.Resolve<TDomainService>(TypedParameter.From(this));
    }

    /// <summary>
    /// 使用 DomainService 领域服务 AOP
    /// </summary>
    /// <remarks>注意：必须在 DomainHost.Initial() 中注册领域服务</remarks>
    /// <typeparam name="TAopContract">领域服务的接口类型</typeparam>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        return DomainHostFactory().EnsureNotNull(name: nameof(DomainHostFactory)).Container.Resolve<TAopContract>(TypedParameter.From(this));
    }

    /// <summary>
    /// Guest 登录为指定用户
    /// </summary>
    /// <typeparam name="TDomainHelperBase"></typeparam>
    /// <param name="userName"></param>
    /// <param name="passwordHashed"></param>
    /// <param name="authenticationType"></param>
    /// <returns></returns>
    public virtual async Task<DomainUser> LoginAsUser<TDomainHelperBase>(string userName, string passwordHashed,
        UserAuthenticationType authenticationType)
    where TDomainHelperBase : DomainHelperBase
    {
        //TODO: 检查当前用户是否为 Guest 用户？
        var session = await DomainHostFactory().UserLoginAsync(
                this, userName, passwordHashed, authenticationType)
            .ConfigureAwait(false);
        SessionKey = session.Key;
        return session.User;
    }

    #region Claims 操作扩展

    /// <summary>
    /// 添加用户身份标识
    /// </summary>
    /// <param name="userName">用户名（不能为空）</param>
    /// <param name="authenticationType">认证类型（Unset 则 IsAuthenticated = false）</param>
    /// <exception cref="ArgumentNullException">用户名不能为空</exception>
    /// <exception cref="ArgumentException">用户名不能仅包含空白字符</exception>
    protected internal void AddUserIdentity(string userName, UserAuthenticationType authenticationType)
    {
        // 1. 显式入参校验：给出明确的异常信息，比隐式的 EnsureHasValue 更易调试
        if (userName == null)
            throw new ArgumentNullException(nameof(userName), "用户名不能为空");
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("用户名不能仅包含空白字符", nameof(userName));

        // 2. 优化：统一用空字符串替代 null（更符合 .NET 框架习惯，行为一致）
        var authTypeString = authenticationType == UserAuthenticationType.Unset
            ? string.Empty
            : authenticationType.ToString();

        // 3. 可选优化：避免重复添加同类型的 Identity（根据业务场景决定是否保留）
        if (Identities.Any(i => i.AuthenticationType == authTypeString))
            return; // 或抛出异常："已存在该类型的身份标识"

        // 4. 正确创建 ClaimsIdentity（IsAuthenticated 由 authTypeString 自动控制）
        var identity = new ClaimsIdentity(authTypeString);

        // 5. 添加用户名声明：先 Trim 再添加，避免首尾空白
        identity.AddClaim(new Claim(ClaimTypes.Name, userName.Trim()));

        // 6. 将新 Identity 添加到 Principal 中
        AddIdentity(identity);
    }

    /// <summary>
    /// 更新指定声明类型的第一个声明的值，如果不存在则添加新声明。
    /// </summary>
    /// <remarks>如果不存在身份标识，则创建新的身份标识。如果已存在指定类型的声明，
    /// 则替换其值；否则，添加新声明。</remarks>
    /// <param name="claimType">要更新的声明的类型。不能为 null、空或仅由空白字符组成。</param>
    /// <param name="value">要分配给声明的新值。如果为 null，则使用空字符串。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="claimType"/> 为 null、空或仅由空白字符组成时抛出。</exception>
    public void UpdateClaim(string claimType, string value)
    {
        if (string.IsNullOrWhiteSpace(claimType))
            throw new ArgumentNullException(nameof(claimType));

        var identity = Identities.FirstOrDefault() ?? new ClaimsIdentity();
        if (!Identities.Contains(identity)) AddIdentity(identity);

        var existing = identity.FindFirst(claimType);
        if (existing != null) identity.RemoveClaim(existing);

        identity.AddClaim(new Claim(claimType, value ?? string.Empty));
    }

    #endregion
}