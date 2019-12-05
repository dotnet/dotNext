using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class TaskResultBinder : CallSiteBinder
    {
        private const string PropertyName = nameof(System.Threading.Tasks.Task<int>.Result);
        private const BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        private Expression Bind(object targetValue, Expression target, LabelTarget returnLabel)
        {
            PropertyInfo? property = targetValue.GetType().GetProperty(PropertyName, PropertyFlags);
            if (property is null)
                target = Expression.Field(null, typeof(Missing), nameof(Missing.Value));
            else
            {
                if (property.PropertyType != target.Type)
                    target = Expression.Convert(target, property.PropertyType);
                target = Expression.Property(target, property);
            }
            target = Expression.Return(returnLabel, target);
            return target;
        }

        public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel) => Bind(args[0], parameters[0], returnLabel);

        public override T BindDelegate<T>(CallSite<T> site, object[] args)
        {
            return base.BindDelegate(site, args);
        }
    }
}
