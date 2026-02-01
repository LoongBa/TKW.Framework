using System;
using System.Threading.Tasks;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话管理器接口
/// 提供会话的创建、获取、更新、激活和放弃等核心功能
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 会话创建事件
    /// 当新会话被创建时触发
    /// </summary>
    event SessionCreated SessionCreated;

    /// <summary>
    /// 会话放弃事件
    /// 当会话被放弃时触发
    /// </summary>
    event SessionAbandon SessionAbandon;

    /// <summary>
    /// 用于保存会话 Key 的 KeyName
    /// </summary>
    string SessionKey_KeyName { get; }

    /// <summary>
    /// 异步创建新会话
    /// </summary>
    /// <returns>返回新创建的会话对象</returns>
    /// <exception cref="SessionException">当会话键无效时抛出</exception>
    Task<SessionInfo> NewSessionAsync();

    /// <summary>
    /// 触发会话创建事件
    /// </summary>
    void OnSessionCreated(SessionInfo session);

    /// <summary>
    /// 异步检查指定会话是否存在
    /// </summary>
    /// <param name="sessionKey">会话 key</param>
    /// <returns>如果会话存在返回true，否则返回false</returns>
    Task<bool> ContainsSessionAsync(string sessionKey);

    /// <summary>
    /// 异步获取指定会话
    /// </summary>
    /// <param name="sessionKey">会话 key</param>
    /// <returns>返回获取到的会话对象</returns>
    /// <exception cref="SessionException">当会话键无效时抛出</exception>
    Task<SessionInfo> GetSessionAsync(string sessionKey);

    /// <summary>
    /// 异步获取并激活指定会话
    /// </summary>
    /// <param name="sessionKey">会话 key</param>
    /// <returns>返回被激活的会话对象</returns>
    /// <exception cref="SessionException">当会话键无效时抛出</exception>
    Task<SessionInfo> GetAndActiveSessionAsync(string sessionKey);

    /// <summary>
    /// 异步放弃指定会话
    /// </summary>
    /// <param name="sessionKey">会话 key</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="SessionException">当会话键无效时抛出</exception>
    Task<SessionInfo> AbandonSessionAsync(string sessionKey);

    /// <summary>
    /// 异步更新并激活指定会话
    /// </summary>
    /// <param name="sessionKey"></param>
    /// <param name="updater"></param>
    /// <returns></returns>
    Task<SessionInfo> UpdateAndActiveSessionAsync(string sessionKey,
        Func<SessionInfo, SessionInfo> updater);
}
