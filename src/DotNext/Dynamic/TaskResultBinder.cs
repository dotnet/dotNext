using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Dynamic
{
    using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

    [RuntimeFeatures(DynamicCodeCompilation = true, RuntimeGenericInstantiation = true)]
    internal sealed class TaskResultBinder : CallSiteBinder
    {
        private const string ResultPropertyName = nameof(Task<Missing>.Result);
        private const BindingFlags ResultPropertyFlags = BindingFlags.Public | BindingFlags.Instance;

        private static Expression BindProperty(PropertyInfo resultProperty, Expression target, out Expression restrictions)
        {
            Debug.Assert(resultProperty.DeclaringType is not null);
            restrictions = Expression.TypeIs(target, resultProperty.DeclaringType);

            // reinterpret reference type without casting because it is protected by restriction
            target = Expression.Call(typeof(Unsafe), nameof(Unsafe.As), new[] { resultProperty.DeclaringType }, target);
            target = Expression.Property(target, resultProperty);
            return target.Type.IsValueType ? Expression.Convert(target, typeof(object)) : target;
        }

#if !NETSTANDARD2_1
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Task<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ValueTask<>))]
#endif
        private static Expression Bind(object targetValue, Expression target, LabelTarget returnLabel)
        {
            PropertyInfo? property = targetValue.GetType().GetProperty(ResultPropertyName, ResultPropertyFlags);
            Debug.Assert(property is not null);
            target = BindProperty(property, target, out var restrictions);

            target = Expression.Return(returnLabel, target);
            target = Expression.Condition(restrictions, target, Expression.Goto(UpdateLabel));
            return target;
        }

        public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel) => Bind(args[0], parameters[0], returnLabel);
    }
}
