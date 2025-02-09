using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace TKW.Framework.Common.Entity
{
    public class OrderExpressionHelperV2
    {
        public static EntityOrderExpression<T> OrderByDescending<T>(Expression<Func<T, object>> expression) where T : class
        {
            return new EntityOrderExpression<T>(expression, ListSortDirection.Descending);
        }

        public static EntityOrderExpression<T> OrderByAscending<T>(Expression<Func<T, object>> expression) where T : class
        {
            return new EntityOrderExpression<T>(expression, ListSortDirection.Ascending);
        }
    }
}
