namespace TKW.Framework.Domain.Interfaces
{
    public interface IUser : IUserInfo
    {
        string UserIdString { get; }
        string UserName { get; }
    }
}