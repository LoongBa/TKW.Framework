using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public interface IDomainUserAccessor<TUserInfo>
    where TUserInfo: class, IUserInfo, new()
{
    public DomainUser<TUserInfo> DomainUser { get; }
}