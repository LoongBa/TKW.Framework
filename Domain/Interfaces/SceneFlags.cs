using System;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// 场景枚举（支持组合）
/// </summary>
[Flags]
public enum SceneFlags
{
    None = 0,
    Create = 1,
    Update = 2,
    Details = 4,
    All = Create | Update | Details
}