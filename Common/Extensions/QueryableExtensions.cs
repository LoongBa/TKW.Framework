using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using TKW.Framework.Common.Entity;

namespace TKW.Framework.Common.Extensions
{
    public static class QueryableExtensions
    {
        /// <summary>
        /// 按指定条件过滤
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IQueryable<T> WhereBy<T>(this IQueryable<T> source, Expression predicate) where T : class
        {
            var query = Expression.Call(typeof(Queryable), "Where", new[] { source.ElementType }, source.Expression, Expression.Quote(predicate));
            return source.Provider.CreateQuery<T>(query);
        }

        /// <summary>
        /// 根据传入的排序表达式数组进行动态排序
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="orderExpressions"></param>
        /// <returns></returns>
        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, IEnumerable<OrderExpression> orderExpressions) where T : class
        {
            if (orderExpressions != null)
            {
                var type = typeof(T);
                var count = 0;
                foreach (var orderExpression in orderExpressions)
                {
                    var propertyName = orderExpression.PropertyName;
                    var property = type.GetProperty(propertyName);
                    var parameter = Expression.Parameter(type, "p");
                    var propertyAccess = Expression.MakeMemberAccess(parameter, property);
                    var orderByExp = Expression.Lambda(propertyAccess, parameter);

                    string methodName;
                    if (count > 0)
                        methodName = orderExpression.Direction == ListSortDirection.Ascending
                                            ? "ThenBy"
                                            : "ThenByDescending";
                    else
                        methodName = orderExpression.Direction == ListSortDirection.Ascending
                                            ? "OrderBy"
                                            : "OrderByDescending";

                    var resultExp = Expression.Call(typeof(Queryable), methodName, new[] { type, property.PropertyType }, source.Expression, Expression.Quote(orderByExp));
                    source = source.Provider.CreateQuery<T>(resultExp);
                    count++;
                }
            }
            return source;
        }
    }
}
