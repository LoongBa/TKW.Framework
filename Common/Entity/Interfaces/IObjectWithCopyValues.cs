namespace TKW.Framework.Common.Entity.Interfaces
{
    /// <summary>
    /// 用于实例之间复制值的辅助方法
    /// </summary>
    /// <remarks>请使用 IEntityModel 的扩展方法</remarks>
    public interface IObjectWithCopyValues<TEntityObject>
        where TEntityObject : IObjectWithCopyValues<TEntityObject>
    {
        /// <summary>
        /// 复制指定 TEntityObject 的值到当前对象
        /// </summary>
        TEntityObject CopyValuesFrom(TEntityObject entity);
        /// <summary>
        /// 复制指定 TEntityObject 的值到当前对象（仅复制用于更新的值）
        /// </summary>
        TEntityObject CopyUpdatableValuesFrom(TEntityObject entity);
    }
}