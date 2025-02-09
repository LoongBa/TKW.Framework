using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace TKW.Framework.Common.Entity
{
    public class EntityOrderExpression<T> : IDisposable
        where T : class
    {
        private readonly List<OrderExpression> _OrderExpressionList;

        public EntityOrderExpression(Expression<Func<T, object>> expression, ListSortDirection direction)
        {
            _OrderExpressionList = new List<OrderExpression>();
            var propertyName = GetPropertyName(expression);
            _OrderExpressionList.Add(
                new OrderExpression
                {
                    PropertyName = propertyName,
                    Direction = direction
                });
        }
        ~EntityOrderExpression()
        {
            _OrderExpressionList.Clear();
        }

        /// <summary>执行与释放或重置非托管资源关联的应用程序定义的任务。</summary>
        public void Dispose()
        {
            _OrderExpressionList.Clear();
        }

        public EntityOrderExpression<T> ThenOrderByAscending(Expression<Func<T, object>> expression)
        {
            var propertyName = GetPropertyName(expression);
            _OrderExpressionList.Add(
                new OrderExpression
                {
                    PropertyName = propertyName,
                    Direction = ListSortDirection.Ascending
                });
            return this;
        }

        public EntityOrderExpression<T> ThenOrderByDescending(Expression<Func<T, object>> expression)
        {
            var propertyName = GetPropertyName(expression);
            _OrderExpressionList.Add(
                new OrderExpression
                {
                    PropertyName = propertyName,
                    Direction = ListSortDirection.Descending
                });
            return this;
        }

        public IEnumerable<OrderExpression> ToList()
        {
            //TODO: Clone
            return new List<OrderExpression>(_OrderExpressionList);
        }

        private string GetPropertyName(Expression<Func<T, object>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            if (memberExpression == null)
            {
                // 再次查一元表达式操作数
                if (expression.Body is UnaryExpression unaryExpression)
                {
                    memberExpression = unaryExpression.Operand as MemberExpression;
                }
            }

            if (memberExpression != null)
            {
                return memberExpression.Member.Name;
            }

            throw new NotSupportedException(nameof(GetPropertyName));
        }
    }
}
