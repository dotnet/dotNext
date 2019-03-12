using System.Linq.Expressions;
using System.Dynamic;

namespace DotNext.Metaprogramming
{
    internal interface IExpressionBuilder<out E>: IDynamicMetaObjectProvider
        where E: Expression
    {
        E Build();
    }
}