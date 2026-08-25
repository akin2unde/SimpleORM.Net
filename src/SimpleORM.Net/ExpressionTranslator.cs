using System.Linq.Expressions;

using System.Security.Cryptography;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

internal static class ExpressionTranslator
{

    public static SearchParam Translate<T>(Expression<Func<T,bool>> e,SearchParam? existing) where T:DBModel
    {
        var p=existing?.Clone()??new();

        Visit(e.Body,p);

        return p;

    }

    private static void Visit(Expression e,SearchParam p)
    {
        if(e is BinaryExpression b&&(b.NodeType==ExpressionType.AndAlso||b.NodeType==ExpressionType.OrElse))
        {
            p.Condition=b.NodeType==ExpressionType.OrElse?SearchCondition.Or:SearchCondition.And;

            Visit(b.Left,p);

            Visit(b.Right,p);

            return;

        }
        if(e is BinaryExpression c&&c.Left is MemberExpression m)
        {
            p.Filters.Add(new SearchFilter
            {
                Field=m.Member.Name,Operator=c.NodeType switch
                {
                    ExpressionType.Equal=>SearchOperator.Equal,ExpressionType.NotEqual=>SearchOperator.NotEqual,ExpressionType.GreaterThan=>SearchOperator.GreaterThan,ExpressionType.GreaterThanOrEqual=>SearchOperator.GreaterThanOrEqual,ExpressionType.LessThan=>SearchOperator.LessThan,ExpressionType.LessThanOrEqual=>SearchOperator.LessThanOrEqual,_=>throw new NotSupportedException()
                }
                ,Value=Expression.Lambda(c.Right).Compile().DynamicInvoke()
            }
            );

            return;

        }
        if(e is MethodCallExpression call&&call.Object is MemberExpression member&&call.Arguments.Count==1)
        {
            p.Filters.Add(new SearchFilter
            {
                Field=member.Member.Name,Operator=call.Method.Name switch
                {
                    nameof(string.Contains)=>SearchOperator.Contains,nameof(string.StartsWith)=>SearchOperator.StartsWith,nameof(string.EndsWith)=>SearchOperator.EndsWith,_=>throw new NotSupportedException()
                }
                ,Value=Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()
            }
            );

            return;

        }
        throw new NotSupportedException($"Expression '{e}' is outside the MVP expression subset.");

    }

}
