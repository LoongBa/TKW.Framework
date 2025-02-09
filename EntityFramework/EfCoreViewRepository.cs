using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKW.Framework.Common.Entity;
using TKW.Framework.Common.Entity.Exceptions;
using TKW.Framework.Common.Entity.Interfaces;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.EntityFramework;

/// <summary>
/// 针对数据库表对象的Helper
/// </summary>
// ReSharper disable once InconsistentNaming
public class EfCoreViewRepository<TDbContext, TEntityModel>
    where TEntityModel : class, IEntityModel
    where TDbContext : DbContext, IEntityDbContext
{
    protected readonly Func<TDbContext> CreateNewDbContext;

    public EfCoreViewRepository(Func<TDbContext> dbContextFactory)
    {
        CreateNewDbContext = dbContextFactory.AssertNotNull(nameof(dbContextFactory));
    }

    public const int DefaultPageSize = 50;
    public const int DefaultMaxPageSize = 500;

    private TDbContext CreateNoTrackingDbContext()
    {
        var context = CreateNewDbContext();
        //自行创建的 DbContext 禁用跟踪提高性能
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return context;
    }

    #region 统计或判断实体是否存在

    /// <summary>
    /// 根据提供的条件统计符合条件的 <typeparamref name="TEntityModel" /> 数量
    /// </summary>
    /// <param name="context"></param>
    /// <param name="expression">条件表达式</param>
    /// <returns>返回统计结果</returns>
    public async Task<bool> AnyAsync(Expression<Func<TEntityModel, bool>> expression, TDbContext context = null)
    {
        var isNewDbContext = context is null;
        var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
        if (isNewDbContext)
            context = CreateNoTrackingDbContext();
        else
            queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

        try
        {
            return expression != null
                ? await context.GetDbSet<TEntityModel>().AnyAsync(expression)
                : await context.GetDbSet<TEntityModel>().AnyAsync();
        }
        finally
        {
            if (isNewDbContext)
                await context.DisposeAsync();
            else
                context.ChangeTracker.QueryTrackingBehavior = queryTrackingBackup;
        }
    }

    /// <summary>
    /// 根据提供的条件统计符合条件的 <typeparamref name="TEntityModel" /> 数量
    /// </summary>
    /// <param name="context"></param>
    /// <param name="expression">条件表达式</param>
    /// <returns>返回统计结果</returns>
    public async Task<int> CountAsync(Expression<Func<TEntityModel, bool>> expression, TDbContext context = null)
    {
        var isNewDbContext = context is null;
        var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
        if (isNewDbContext)
            context = CreateNoTrackingDbContext();
        else
            queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

        try
        {
            return expression != null
                ? await context.GetDbSet<TEntityModel>().CountAsync(expression)
                : await context.GetDbSet<TEntityModel>().CountAsync();
        }
        finally
        {
            if (isNewDbContext)
                await context.DisposeAsync();
            else
                context.ChangeTracker.QueryTrackingBehavior = queryTrackingBackup;
        }
    }

    #endregion

    #region 根据条件选择 TEntity

    /// <summary>
    /// 根据条件返回 <typeparamref name="TEntityModel" /> 的实体集合
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderExpressions">排序表达式</param>
    /// <param name="topCount">最大选取记录数</param>
    /// <param name="context"></param>
    /// <typeparam name="TEntityModel">实体</typeparam>
    /// <returns>实体集合</returns>
    public async Task<List<TEntityModel>> WhereAsync(
    Expression<Func<TEntityModel, bool>> whereExpression,
    IQueryable<OrderExpression> orderExpressions, 
    int topCount, TDbContext context = null)
    {
        return await PagedWhereAsync(whereExpression, orderExpressions, Pager.New(topCount), context);
    }

    /// <summary>
    /// 根据条件返回 <typeparamref name="TEntityModel" /> 的实体集合
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderExpressions">排序表达式</param>
    /// <param name="context"></param>
    /// <returns>实体集合</returns>
    public async Task<List<TEntityModel>> WhereAsync(
        Expression<Func<TEntityModel, bool>> whereExpression,
        IQueryable<OrderExpression> orderExpressions,
        TDbContext context = null)
    {
        return await WhereAsync(whereExpression, orderExpressions, 0, context);
    }

    /// <summary>
    /// 根据条件返回 <typeparamref name="TEntityModel" /> 的实体集合
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="topCount"></param>
    /// <param name="context"></param>
    /// <returns>实体集合</returns>
    public async Task<List<TEntityModel>> WhereAsync(
        Expression<Func<TEntityModel, bool>> whereExpression,
        int topCount, TDbContext context = null)
    {
        return await WhereAsync(whereExpression, null, topCount, context);
    }

    /// <summary>
    /// 根据条件返回 <typeparamref name="TEntityModel" /> 的实体集合
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="topCount"></param>
    /// <param name="context"></param>
    /// <returns>实体集合</returns>
    public async Task<List<TEntityModel>> WhereAsync(
        Expression<Func<TEntityModel, bool>> whereExpression,
        TDbContext context = null)
    {
        return await WhereAsync(whereExpression, null, 0, context);
    }

    #region 返回符合条件的第一个Entity(未找到时抛出EntityNotFoundException)

    /// <summary>
    /// 返回符合条件的第一个 <typeparamref name="TEntityModel" />
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderExpressions">排序子句</param>
    /// <param name="context"></param>
    /// <param name="throwExceptionWhenNotExists"></param>
    /// <exception cref="EntityNotFoundException">未找到返回符合条件的数据时产生该异常</exception>
    /// <returns>返回T</returns>
    public async Task<TEntityModel> RetrieveAsync(
        Expression<Func<TEntityModel, bool>> whereExpression,
        IQueryable<OrderExpression> orderExpressions,
        TDbContext context = null,
        bool throwExceptionWhenNotExists = true)
    {
        var isNewDbContext = context is null;
        var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
        if (isNewDbContext)
            context = CreateNoTrackingDbContext();
        else
            queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

        try
        {
            var models = context.GetDbSet<TEntityModel>().AsQueryable();

            //根据传入的条件表达式进行过滤
            if (whereExpression != null)
                models = models.WhereBy(whereExpression);
            //根据传入的排序表达式数组进行动态排序
            if (orderExpressions != null)
                models = models.OrderBy(orderExpressions);

            var model = await models.FirstOrDefaultAsync();
            if (throwExceptionWhenNotExists && model == null)
                throw new EntityNotFoundException(typeof(TEntityModel).Name);

            return model;
        }
        finally
        {
            if (isNewDbContext)
                await context.DisposeAsync();
            else
                context.ChangeTracker.QueryTrackingBehavior = queryTrackingBackup;
        }
    }

    /// <summary>
    /// 返回符合条件的第一个 <typeparamref name="TEntityModel" />
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="context"></param>
    /// <exception cref="EntityNotFoundException">未找到返回符合条件的数据时产生该异常</exception>
    /// <returns>返回T</returns>
    public async Task<TEntityModel> RetrieveAsync(Expression<Func<TEntityModel, bool>> whereExpression,
        TDbContext context = null)
    {
        return await RetrieveAsync(whereExpression, null, context);
    }
    #endregion

    #region 返回符合条件的第一个Entity(未找到时返回 null )

    /// <summary>
    /// 返回符合条件的第一个 <typeparamref name="TEntityModel" />，未找到时返回null
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderExpressions">排序子句</param>
    /// <param name="context"></param>
    /// <returns>返回T</returns>
    public async Task<TEntityModel> RetrieveOrDefaultAsync(
        Expression<Func<TEntityModel, bool>> whereExpression, IQueryable<OrderExpression> orderExpressions,
        TDbContext context = null)
    {
        return await RetrieveAsync(whereExpression, orderExpressions, context, false);
    }

    /// <summary>
    /// 返回符合条件的第一个 <typeparamref name="TEntityModel" />，未找到时返回null
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="context"></param>
    /// <returns>返回<typeparamref name="TEntityModel" /></returns>
    public async Task<TEntityModel> RetrieveOrDefaultAsync(
        Expression<Func<TEntityModel, bool>> whereExpression, TDbContext context = null)
    {
        return await RetrieveOrDefaultAsync(whereExpression, null, context);
    }
    #endregion


    #endregion

    #region 根据条件选择 TEntity 并自动分页

    /// <summary>
    /// 根据条件返回 <typeparamref name="TEntityModel" /> 的实体集合（分页）
    /// </summary>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="orderExpressions">排序子句</param>
    /// <param name="pager">分页条件</param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<List<TEntityModel>> PagedWhereAsync(
        Expression<Func<TEntityModel, bool>> whereExpression,
        IQueryable<OrderExpression> orderExpressions, Pager pager,
        TDbContext context = null)
    {
        var isNewDbContext = context is null;
        var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
        if (isNewDbContext)
            context = CreateNoTrackingDbContext();
        else
            queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

        try
        {
            var models = context.GetDbSet<TEntityModel>().AsQueryable();

            //根据传入的条件表达式进行过滤
            if (whereExpression != null)
                models = models.WhereBy(whereExpression);
            //根据传入的排序表达式数组进行动态排序
            if (orderExpressions != null)
                models = models.OrderBy(orderExpressions);

            pager.TotalCount = await CountAsync(whereExpression, context);
            return await models.Skip(pager.Skip).Take(pager.PageSize).ToListAsync();
        }
        finally
        {
            if (isNewDbContext)
                await context.DisposeAsync();
            else
                context.ChangeTracker.QueryTrackingBehavior = queryTrackingBackup;
        }
    }
    #endregion
}