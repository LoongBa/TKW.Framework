using System;
using System.Linq.Expressions;
using TKW.Framework.Common.Entity.Exceptions;
using TKW.Framework.Common.Entity.Interfaces;
using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Common.Entity
{
    public static class EntityStateHelper
    {
        #region IsDeleted

        /// <summary>
        /// 构造针对删除状态的查询表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="deletedState"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> BuildDeletedStateExpression<T>(EnumIsDeletedStateType deletedState = EnumIsDeletedStateType.UnDeleted)
            where T : class, IEntityHasIsDeletedState
        {
            Expression<Func<T, bool>> whereExpression = deletedState switch
            {
                EnumIsDeletedStateType.All => p => true,
                EnumIsDeletedStateType.Deleted => p => p.IsDeleted,
                EnumIsDeletedStateType.UnDeleted => p => !p.IsDeleted,
                _ => throw new ArgumentOutOfRangeException(nameof(deletedState), deletedState, null)
            };

            return whereExpression;
        }

        public static EnumIsDeletedStateType CheckIsDeletedState(EnumIsDeletedStateType state, IEntityHasIsDeletedState model)
        {
            if (state == EnumIsDeletedStateType.All) return EnumIsDeletedStateType.All; // 当输入为All时，不关心entity是否删除
            var deletedState = model.IsDeleted ? EnumIsDeletedStateType.Deleted : EnumIsDeletedStateType.UnDeleted;
            return state == deletedState ? EnumIsDeletedStateType.All : deletedState;
        }

        /// <summary>
        /// 附加针对删除状态的查询表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="deletedState"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> AndDeletedStateIs<T>(this Expression<Func<T, bool>> left, EnumIsDeletedStateType deletedState)
            where T : class, IEntityHasIsDeletedState
        {
            var exp = BuildDeletedStateExpression<T>(deletedState);
            return left == null ? exp : left.And(exp);
        }

        /// <summary>
        /// 确保实体未删除
        /// </summary>
        /// <exception cref="EntityStateException">实体状态异常</exception>
        public static void MakeSureUnDeleted<T>(this T left)
            where T : class, IEntityHasIsDeletedState
        {
            if (left.IsDeleted)
                throw new EntityStateException(nameof(left), EntityStateExceptionType.EntityIsDeleted);
        }

        #endregion

        #region IsEnabled

        /// <summary>
        /// 构造针对启用/激活状态的查询表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> BuildEnabledStateExpression<T>(EnumEnableDisableStateType state = EnumEnableDisableStateType.Enabled)
            where T : class, IEntityHasIsEnabledState
        {
            Expression<Func<T, bool>> whereExpression = state switch
            {
                EnumEnableDisableStateType.All => p => true,
                EnumEnableDisableStateType.Disabled => p => !p.IsEnabled,
                EnumEnableDisableStateType.Enabled => p => p.IsEnabled,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            return whereExpression;
        }

        public static EnumEnableDisableStateType CheckIsEnabledState(EnumEnableDisableStateType state, IEntityHasIsEnabledState model)
        {
            if (state == EnumEnableDisableStateType.All) return EnumEnableDisableStateType.All; // 当输入为All时，不关心entity是否启用
            var enabledState = model.IsEnabled ? EnumEnableDisableStateType.Enabled : EnumEnableDisableStateType.Disabled;
            return state == enabledState ? EnumEnableDisableStateType.All : enabledState;
        }

        /// <summary>
        /// 附加针对启用/激活状态的查询表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> AndEnabledStateIs<T>(this Expression<Func<T, bool>> left, EnumEnableDisableStateType state)
            where T : class, IEntityHasIsEnabledState
        {
            var exp = BuildEnabledStateExpression<T>(state);
            return left == null ? exp : left.And(exp);
        }

        /// <summary>
        /// 确保实体未删除
        /// </summary>
        /// <exception cref="EntityStateException">实体状态异常</exception>
        public static void MakeSureEnabled<T>(this T left)
            where T : class, IEntityHasIsEnabledState
        {
            if (!left.IsEnabled)
                throw new EntityStateException(nameof(left), EntityStateExceptionType.EntityIsDisabled);
        }
        #endregion
    }
}