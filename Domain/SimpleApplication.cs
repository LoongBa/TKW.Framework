using System;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

/// <summary>
/// 应用程序
/// </summary>
public sealed class SimpleApplication : IApplication
{
    public SimpleApplication(Guid uid, string username)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentNullException(nameof(username));
        Uid = uid;
        Name = username;
    }

    public SimpleApplication(Guid uuid, string username, string version, string description)
        : this(uuid, username)
    {
        Version = version;
        Description = description;
    }

    public Guid Uid { get; }
    public string Name { get; }
    public string Version { get; } = string.Empty;
    public string Description { get; } = string.Empty;
}