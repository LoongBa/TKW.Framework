using TKW.Framework.Common.Entity.History;

namespace TKW.Framework.Common.Entity.Interfaces
{
    public interface IEntityHistory<TEntity> : IEntityBase
        where TEntity : IEntityHistory<TEntity>
    {
        /// <summary>
        /// 用于实体历史记录的对象
        /// </summary>
        EntityHistoryHelper<TEntity> EntityHistory { get; }
    }
}