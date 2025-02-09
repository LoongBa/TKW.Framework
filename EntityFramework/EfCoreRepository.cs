using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TKW.Framework.Common.Entity;
using TKW.Framework.Common.Entity.Interfaces;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Common.Validation;

namespace TKW.Framework.EntityFramework
{
    public class EfCoreRepository<TDbContext, TEntityModel> : EfCoreViewRepository<TDbContext, TEntityModel>
        where TEntityModel : class, IEntityModifiable, IEntityModel
        where TDbContext : DbContext, IEntityDbContext
    {
        public EfCoreRepository(Func<TDbContext> dbContextFactory) : base(dbContextFactory)
        {
        }

        #region Create

        /// <summary>
        /// 创建新的 <typeparamref name="TEntityModel" /> 
        /// </summary>
        /// <param name="model">要创建的 <typeparamref name="TEntityModel" /></param>
        /// <param name="context"></param>
        public async Task<TEntityModel> CreateAsync(TEntityModel model, TDbContext context = null)
        {
            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            try
            {
                model.ValidateAndThrow();
                context.GetDbSet<TEntityModel>().Add(model);
                await context.SaveChangesAsync();
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
        /// 批量创建
        /// </summary>
        /// <param name="models"></param>
        /// <param name="context"></param>
        /// <remarks>批量操作建议使用：Zack.EFCore.Batch https://github.com/yangzhongke/Zack.EFCore.Batch 或等待 EF7</remarks>
        /// <returns></returns>
        public async Task<List<TEntityModel>> BatchCreateAsync(List<TEntityModel> models,
            TDbContext context = null)
        {
            models.AssertNotNull(nameof(models));

            foreach (var model in models)
                model.ValidateAndThrow();

            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            try
            {
                foreach (var model in models)
                    context.Entry(model).State = EntityState.Added;
                await context.SaveChangesAsync();
                return models;
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

        #region Update Method

        /// <summary>
        /// 更新T（从传入的 <typeparamref name="TEntityModel" />  中获取相关参数）
        /// </summary>
        /// <param name="model">要更新的 <typeparamref name="TEntityModel" />  </param>
        /// <param name="context"></param>
        public async Task<TEntityModel> UpdateOneAsync(TEntityModel model, TDbContext context = null)
        {
            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            try
            {
                model.ValidateAndThrow();
                context.Update(model);
                await context.SaveChangesAsync();
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
        /// 批量更新
        /// </summary>
        /// <param name="models"></param>
        /// <param name="context"></param>
        /// <remarks>批量操作建议使用：Zack.EFCore.Batch https://github.com/yangzhongke/Zack.EFCore.Batch 或等待 EF7</remarks>
        /// <returns></returns>
        public async Task<List<TEntityModel>> BatchUpdateAsync(List<TEntityModel> models, TDbContext context = null)
        {
            //TODO: 改为其它批量更新，或等 EFCore7 的批量更新
            models.AssertNotNull(nameof(models));

            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            foreach (var model in models)
                model.ValidateAndThrow();

            try
            {
                foreach (var model in models)
                    context.Update(model);

                await context.SaveChangesAsync();
                return models;
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

        #region Remove

        /// <summary>
        /// 根据条件删除符合条件的 <typeparamref name="TEntityModel" />  
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="orderExpressions"></param>
        /// <param name="context"></param>
        /// <returns>返回删除的记录个数</returns>
        public async Task<TEntityModel> RemoveFirstOneAsync(Expression<Func<TEntityModel, bool>> whereExpression,
            IQueryable<OrderExpression> orderExpressions,
            TDbContext context = null)
        {
            whereExpression.AssertNotNull(nameof(whereExpression));

            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            var delModel = await RetrieveAsync(whereExpression, orderExpressions, context);
            try
            {
                var models = context.GetDbSet<TEntityModel>().AsQueryable();
                context.GetDbSet<TEntityModel>().Remove(delModel);
                await context.SaveChangesAsync();
                return delModel;
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
        /// 根据条件删除符合条件的 <typeparamref name="TEntityModel" />  
        /// </summary>
        /// <param name="whereExpression">条件表达式</param>
        /// <param name="context"></param>
        /// <remarks>批量操作建议使用：Zack.EFCore.Batch https://github.com/yangzhongke/Zack.EFCore.Batch 或等待 EF7</remarks>
        /// <returns>返回删除的记录</returns>
        public async Task<List<TEntityModel>> RemoveAsync(Expression<Func<TEntityModel, bool>> whereExpression,
            TDbContext context = null)
        {
            //TODO: 改为其它批量删除，或等 EFCore7 的批量删除
            var isNewDbContext = context is null;
            var queryTrackingBackup = QueryTrackingBehavior.TrackAll;
            if (isNewDbContext)
                context = CreateNewDbContext();
            else
                queryTrackingBackup = context.ChangeTracker.QueryTrackingBehavior;

            try
            {
                var models = context.GetDbSet<TEntityModel>().AsQueryable();
                var delModels = await WhereAsync(whereExpression, context);
                context.GetDbSet<TEntityModel>().RemoveRange(delModels);
                await context.SaveChangesAsync();
                return delModels;
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
}
