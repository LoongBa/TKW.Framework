using System;

namespace xCodeGen.Abstractions
{
    /// <summary>
    /// 场景枚举（支持组合）
    /// </summary>
    [Flags]
    public enum EnumSceneFlags
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 创建
        /// </summary>
        Create = 1,
        /// <summary>
        /// 更新
        /// </summary>
        Update = 2,
        /// <summary>
        /// 详情
        /// </summary>
        Details = 4,
        /// <summary>
        /// 强制验证
        /// </summary>
        ForceValidate = 8 // 系统自检/审计等特殊场景，强制验证且不区分持久化来源
    }
}