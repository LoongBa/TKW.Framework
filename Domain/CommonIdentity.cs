using System.Security.Principal;

namespace TKW.Framework.Domain;

/// <inheritdoc />
/// <summary>
/// 用户身份基类
/// </summary>
public class CommonIdentity(string name, string authenticationType, bool isAuthenticated)
    : IIdentity
{
    public string AuthenticationType { get; } = authenticationType;

    public string Name { get; } = name;

    public bool IsAuthenticated { get; } = isAuthenticated;
}