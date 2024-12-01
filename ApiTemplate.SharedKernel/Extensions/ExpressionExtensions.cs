using System.Linq.Expressions;

namespace ApiTemplate.SharedKernel.Extensions
{
    public static class ExpressionExtensions
    {
        // from package LinqKit.Core, http://www.albahari.com/nutshell/predicatebuilder.aspx
        private class RebindParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;

            private readonly ParameterExpression _newParameter;

            public RebindParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _oldParameter)
                {
                    return _newParameter;
                }

                return base.VisitParameter(node);
            }
        }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
        {
            Expression right = new RebindParameterVisitor(expr2.Parameters[0], expr1.Parameters[0]).Visit(expr2.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.OrElse(expr1.Body, right), expr1.Parameters);
        }

        public enum Conditions
        {
            Less = 1,
            Between,
            Greater,
        }

        public static IQueryable<TSource> WhereCompare<TSource, TResult, T1>(
            this IQueryable<TSource> source,
            Expression<Func<TSource, TResult>> predicate,
            Conditions condition,
            T1 Value,
            object Value2 = null)
        {
            var l = predicate.Body;
            var r = Expression.Constant(Value, l.Type);
            var body = condition switch
            {
                Conditions.Less => Expression.LessThan(l, r),
                Conditions.Greater => Expression.GreaterThan(l, r),
                Conditions.Between => Expression.And(Expression.LessThanOrEqual(l, r), Expression.GreaterThanOrEqual(l, Expression.Constant(Value2))),
                _ => throw new NotSupportedException()
            };
            return source.Where(Expression.Lambda<Func<TSource, bool>>(body, predicate.Parameters));
        }
    }
}
