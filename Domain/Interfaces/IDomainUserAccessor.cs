namespace TKW.Framework.Domain.Interfaces;

public interface IDomainUserAccessor<TUserInfo>
    where TUserInfo: class, IUserInfo, new()
{
    public DomainUser<TUserInfo> DomainUser { get; }
}