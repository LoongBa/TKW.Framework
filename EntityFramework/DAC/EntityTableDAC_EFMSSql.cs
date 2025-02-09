using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TKW.Framework.Common.Entity;
using TKW.Framework.Common.Entity.Exceptions;
using TKW.Framework.Common.Entity.Interfaces;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.EntityFramework.DAC
{
    /// <summary>
    /// 针对数据库表对象的Helper
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    // ReSharper disable once InconsistentNaming
    public class EntityTableDAC_EFMSSql<TEntity, TDbContext>
        where TEntity : class, IEntityValidatable
        where TDbContext : DbContext, IEntityDbContext
    {
        public const int DefaultPageSize = 300;

        private readonly IEntityDaHelper<TDbContext> _Helper;

        public EntityTableDAC_EFMSSql(IEntityDaHelper<TDbContext> helper)
        {
            _Helper = helper;
        }

        #region 统计或判断实体是否存在

        /// <summary>
        /// 根据提供的条件统计符合条件的 <typeparamref name="TEntity" /> 数量
        /// </summary>
        /// <param name="expression">条件表达式</param>
        /// <param name="context">上下文环境（用于共享事务）</param>
        /// <returns>返回统计结果</returns>
        public int CountWhere(Expression<Func<TEntity, bool>> expression, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            var result = context.GetDbSet<TEntity>().Count(expression);

            innerContext?.Dispose();

            return result;
        }

        #endregion

        #region 根据条件选择 TEntity
        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序表达式</param>
        /// <param name="topCount">最大选取记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <typeparam name="TEntity">实体</typeparam>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression<Func<TEntity, bool>> whereExpression,
            IEnumerable<OrderExpression> orderExpressions, int topCount, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null) entities = entities.Where(whereExpression);

            //根据传入的排序表达式数组进行动态排序
            entities = entities.OrderBy(orderExpressions);

            //取顶部若干项
            if (topCount > 0)
                entities = entities.Take(topCount);

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序表达式</param>
        /// <param name="topCount">最大选取记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <typeparam name="TEntity">实体</typeparam>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression whereExpression,
            IEnumerable<OrderExpression> orderExpressions, int topCount, TDbContext context = null)
        {
            if (orderExpressions == null) throw new ArgumentNullException(nameof(orderExpressions));

            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null) entities = entities.WhereBy(whereExpression);

            //根据传入的排序表达式数组进行动态排序
            entities = entities.OrderBy(orderExpressions);

            //取顶部若干项
            if (topCount > 0)
                entities = entities.Take(topCount);

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序表达式</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression<Func<TEntity, bool>> whereExpression, IEnumerable<OrderExpression> orderExpressions, TDbContext context = null)
        {
            return SelectWhere(whereExpression, orderExpressions, 0, context);
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序表达式</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression whereExpression, IEnumerable<OrderExpression> orderExpressions, TDbContext context = null)
        {
            return SelectWhere(whereExpression, orderExpressions, 0, context);
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression<Func<TEntity, bool>> whereExpression, TDbContext context = null)
        {
            return SelectWhere(whereExpression, null, 0, context);
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWhere(Expression whereExpression, TDbContext context = null)
        {
            return SelectWhere(whereExpression, null, 0, context);
        }

        #region 返回符合条件的第一个Entity(未找到时抛出EntityNotFoundException)
        /// <summary>
        /// 返回符合条件的第一个 <typeparamref name="TEntity" />
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="context">相关的 DbContext</param>
        /// <exception cref="EntityNotFoundException">未找到返回符合条件的数据时产生该异常</exception>
        /// <returns>返回T</returns>
        public TEntity RetrieveWhere(Expression<Func<TEntity, bool>> whereExpression, IEnumerable<OrderExpression> orderExpressions, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            try
            {
                TEntity model;
                if (whereExpression == null)
                {
                    model = context.GetDbSet<TEntity>().FirstOrDefault();
                    if (model == null)
                        throw new EntityNotFoundException(typeof(TEntity).Name);
                    return model;
                }

                var entities = context.GetDbSet<TEntity>().Where(whereExpression);
                entities = entities.OrderBy(orderExpressions);

                model = entities.FirstOrDefault();
                if (model == null)
                    throw new EntityNotFoundException(typeof(TEntity).Name);

                return model;
            }
            finally
            {
                innerContext?.Dispose();
            }
        }

        /// <summary>
        /// 返回符合条件的第一个 <typeparamref name="TEntity" />
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context">相关的 DbContext</param>
        /// <exception cref="EntityNotFoundException">未找到返回符合条件的数据时产生该异常</exception>
        /// <returns>返回T</returns>
        public TEntity RetrieveWhere(Expression<Func<TEntity, bool>> whereExpression, TDbContext context = null)
        {
            return RetrieveWhere(whereExpression, null, context);
        }
        #endregion

        #region 返回符合条件的第一个Entity(未找到时返回null)
        /// <summary>
        /// 返回符合条件的第一个 <typeparamref name="TEntity" />，未找到时返回null
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="context">相关的 DbContext</param>
        /// <returns>返回T</returns>
        public TEntity RetrieveWhereOrDefault(Expression<Func<TEntity, bool>> whereExpression, IEnumerable<OrderExpression> orderExpressions, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            try
            {
                if (whereExpression == null) return context.GetDbSet<TEntity>().FirstOrDefault();

                var entities = context.GetDbSet<TEntity>().Where(whereExpression);
                entities = entities.OrderBy(orderExpressions);

                return entities.FirstOrDefault();
            }
            finally
            {
                innerContext?.Dispose();
            }
        }

        /// <summary>
        /// 返回符合条件的第一个 <typeparamref name="TEntity" />，未找到时返回null
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context">相关的 DbContext</param>
        /// <returns>返回<typeparamref name="TEntity" /></returns>
        public TEntity RetrieveWhereOrDefault(Expression<Func<TEntity, bool>> whereExpression, TDbContext context = null)
        {
            return RetrieveWhereOrDefault(whereExpression, null, context);
        }
        #endregion


        #endregion

        #region 根据条件选择 TEntity 并自动分页

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="pageNumber">页码</param>
        /// <param name="pageCount">计算返回的总页数</param>
        /// <param name="recordsCount">符合条件的记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression<Func<TEntity, bool>> whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            int pageSize,
            ref int pageNumber,
            out int pageCount,
            out int recordsCount,
            TDbContext context = null
            )
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            recordsCount = CountWhere(whereExpression, context);
            if (pageSize <= 0) pageSize = DefaultPageSize;
            int mod = recordsCount % pageSize;
            if (mod > 0)
                pageCount = recordsCount / pageSize + 1;
            else
                pageCount = recordsCount / pageSize;

            if (pageNumber <= 0)
                pageNumber = 1;
            else
                if (pageNumber > pageCount) pageNumber = pageCount;

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null)
                entities = entities.Where(whereExpression);

            entities = entities.OrderBy(orderExpressions);

            if (pageNumber > 1)
                entities = entities.Skip((pageNumber - 1) * pageSize);

            mod = recordsCount - pageSize * (pageNumber - 1);
            entities = entities.Take(mod < pageSize ? mod : pageSize);

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="pageNumber">页码</param>
        /// <param name="pageCount">计算返回的总页数</param>
        /// <param name="recordsCount">符合条件的记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            int pageSize,
            ref int pageNumber,
            out int pageCount,
            out int recordsCount,
            TDbContext context = null
            )
        {
            if (orderExpressions == null) throw new ArgumentNullException(nameof(orderExpressions));

            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null)
                entities = entities.WhereBy(whereExpression);

            recordsCount = entities.Count();
            if (pageSize <= 0) pageSize = DefaultPageSize;
            int mod = recordsCount % pageSize;
            if (mod > 0)
                pageCount = recordsCount / pageSize + 1;
            else
                pageCount = recordsCount / pageSize;

            if (pageNumber <= 0)
                pageNumber = 1;
            else
                if (pageNumber > pageCount) pageNumber = pageCount;

            entities = entities.OrderBy(orderExpressions);

            if (pageNumber > 1)
                entities = entities.Skip((pageNumber - 1) * pageSize);

            mod = recordsCount - pageSize * (pageNumber - 1);
            entities = entities.Take(mod < pageSize ? mod : pageSize);

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="start">从第start条开始</param>
        /// <param name="limit">取limit条</param>
        /// <param name="recordsCount">符合条件的记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression<Func<TEntity, bool>> whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            int start,
            int limit,
            out int recordsCount,
            TDbContext context = null
            )
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            recordsCount = CountWhere(whereExpression, context);

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null)
                entities = entities.Where(whereExpression);

            entities = entities.OrderBy(orderExpressions);

            if (limit <= 0)
            {
                entities = entities.Skip(start);
            }
            else
            {
                entities = entities.Skip(start).Take(limit);
            }

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="pager">分页条件</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression<Func<TEntity, bool>> whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            Pager pager,
            TDbContext context = null
            )
        {
            var start = pager.PageSize * pager.PageIndex;
            var limit = pager.PageSize;
            int total;
            var entities = SelectWherePaged(whereExpression, orderExpressions, start, limit, out total, context);
            pager.TotalCount = total;
            return entities;
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="start">从第start条开始</param>
        /// <param name="limit">取limit条</param>
        /// <param name="recordsCount">符合条件的记录数</param>
        /// <param name="context">相关的 DbContext，如果此参数不为空，仅定义查询，不立即执行</param>
        /// <returns>实体集合</returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            int start,
            int limit,
            out int recordsCount,
            TDbContext context = null
            )
        {
            if (orderExpressions == null) throw new ArgumentNullException(nameof(orderExpressions));

            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression != null)
                entities = entities.WhereBy(whereExpression);

            entities = entities.OrderBy(orderExpressions);

            recordsCount = entities.Count();

            if (limit <= 0)
            {
                entities = entities.Skip(start);
            }
            else
            {
                entities = entities.Skip(start).Take(limit);
            }

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="pager">分页条件</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression whereExpression,
            IEnumerable<OrderExpression> orderExpressions,
            Pager pager,
            TDbContext context = null
            )
        {
            var start = pager.PageSize * pager.PageIndex;
            var limit = pager.PageSize;
            int total;
            var entities = SelectWherePaged(whereExpression, orderExpressions, start, limit, out total, context);
            pager.TotalCount = total;
            return entities;
        }

        /// <summary>
        /// 根据条件返回 <typeparamref name="TEntity" /> 的实体集合（分页）
        /// </summary>
        /// <param name="whereExpression1">条件表达式1</param>
        /// <param name="whereExpression2">条件表达式2</param>
        /// <param name="orderExpressions">排序子句</param>
        /// <param name="pager">分页条件</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<TEntity> SelectWherePaged(
            Expression<Func<TEntity, bool>> whereExpression1,
            Expression whereExpression2,
            IEnumerable<OrderExpression> orderExpressions,
            Pager pager,
            TDbContext context = null
            )
        {
            if (whereExpression2 != null && orderExpressions == null) throw new ArgumentNullException(nameof(orderExpressions));

            var start = pager.PageSize * pager.PageIndex;
            var limit = pager.PageSize;

            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            IQueryable<TEntity> entities = context.GetDbSet<TEntity>();
            if (whereExpression1 != null) entities = entities.Where(whereExpression1);
            if (whereExpression2 != null) entities = entities.WhereBy(whereExpression2);

            entities = entities.OrderBy(orderExpressions);

            pager.TotalCount = entities.Count();

            if (limit <= 0)
            {
                entities = Queryable.Skip(entities, start);
            }
            else
            {
                entities = Queryable.Take<TEntity>(entities.Skip(start), limit);
            }

            if (innerContext != null)
            {
                var result = entities.ToList();
                innerContext.Dispose();
                return result;
            }
            else
            {
                return entities;
            }
        }

        #endregion

        #region 加载导航属性
        /// <summary>
        /// 加载 <typeparamref name="TEntity" /> 指定的导航属性
        /// </summary>
        /// <param name="context">相关的 DbContext</param>
        /// <param name="entity">要加载导航属性的 <typeparamref name="TEntity" /> 实体</param>
        /// <param name="navigationProperty">导航属性</param>
        public void Reference<TProperty>(TDbContext context, TEntity entity, Expression<Func<TEntity, TProperty>> navigationProperty) where TProperty : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Entry(entity).Reference(navigationProperty).Load();
        }

        /// <summary>
        /// 加载 <typeparamref name="TEntity" /> 指定的集合导航属性
        /// </summary>
        /// <param name="context">相关的 DbContext</param>
        /// <param name="entity">要加载集合导航属性的 <typeparamref name="TEntity" /> 实体</param>
        /// <param name="navigationProperty">集合导航属性</param>
        public void Collection<TElement>(TDbContext context, TEntity entity, Expression<Func<TEntity, IEnumerable<TElement>>> navigationProperty) where TElement : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Entry(entity).Collection(navigationProperty).Load();
        }

        /// <summary>
        /// 返回 <typeparamref name="TEntity" /> 指定的可查询集合导航属性
        /// </summary>
        /// <param name="context">相关的 DbContext</param>
        /// <param name="entity">查询集合导航属性的 <typeparamref name="TEntity" /> 实体</param>
        /// <param name="navigationProperty">可查询的集合导航属性</param>
        public IQueryable<TElement> QueryableNavigationProperty<TElement>(TDbContext context, TEntity entity, Expression<Func<TEntity, IEnumerable<TElement>>> navigationProperty) where TElement : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return context.Entry(entity).Collection(navigationProperty).Query();
        }

        #endregion

        #region 创建新 TEntity

        /// <summary>
        /// 创建新的 <typeparamref name="TEntity" /> 
        /// </summary>
        /// <param name="entity">要创建的 <typeparamref name="TEntity" /></param>
        /// <param name="context">相关的 DbContext</param>
        /// <returns>调用是否成功</returns>
        public TEntity Create(TEntity entity, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            entity.ValidateValues();
            context.Entry(entity).State = EntityState.Added;
            context.SaveChanges();

            innerContext?.Dispose();
            return entity;
        }
        /// <summary>
        /// 批量创建
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public IList<TEntity> BatchCreate(IList<TEntity> entities, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }
            foreach (var entity in entities)
            {
                entity.ValidateValues();
                context.Entry(entity).State = EntityState.Added;
            }

            context.SaveChanges();

            innerContext?.Dispose();
            return entities;
        }
        #endregion

        #region 根据条件删除符合条件的 TEntity

        /// <summary>
        /// 根据条件删除符合条件的 <typeparamref name="TEntity" />  
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context">相关的 DbContext</param>
        /// <returns>返回删除的记录个数</returns>
        public int RemoveWhere(Expression<Func<TEntity, bool>> whereExpression, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            var entities = context.GetDbSet<TEntity>().Where(whereExpression);
            foreach (var entity in entities)
                context.Entry(entity).State = EntityState.Deleted;

            var result = context.SaveChanges();

            innerContext?.Dispose();

            return result;
        }
        #endregion

        #region 更新 TEntity

        /// <summary>
        /// 更新T（从传入的 <typeparamref name="TEntity" />  中获取相关参数）
        /// </summary>
        /// <param name="entity">要更新的 <typeparamref name="TEntity" />  </param>
        /// <param name="context">相关的 DbContext</param>
        public TEntity Update(TEntity entity, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }

            entity.ValidateValues();
            context.Set<TEntity>().Attach(entity);
            context.Entry(entity).State = EntityState.Modified;
            context.SaveChanges();

            innerContext?.Dispose();

            return entity;
        }
        /// <summary>
        /// 批量更新
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public List<TEntity> BatchUpdate(List<TEntity> entities, TDbContext context = null)
        {
            TDbContext innerContext = null;
            if (context == null)
            {
                innerContext = _Helper.NewDbContext();
                context = innerContext;
            }
            foreach (var entity in entities)
            {
                entity.ValidateValues();
                context.Set<TEntity>().Attach(entity);
                context.Entry(entity).State = EntityState.Modified;
            }

            context.SaveChanges();

            innerContext?.Dispose();
            return entities;
        }
        #endregion
    }
}
