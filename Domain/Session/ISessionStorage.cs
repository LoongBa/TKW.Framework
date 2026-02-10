using System.Threading.Tasks;

namespace TKW.Framework.Domain.Session;

public interface ISessionStorage
{
    Task<string?> GetSessionKeyAsync();
    Task SaveSessionKeyAsync(string sessionKey);
    Task ClearSessionKeyAsync();
    Task<bool> HasSessionKeyAsync();
}