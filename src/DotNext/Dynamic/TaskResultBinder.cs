﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Dynamic;

[RequiresDynamicCode("DLR is required to resolve underlying task type at runtime")]
internal sealed class TaskResultBinder : CallSiteBinder
{
    private static Expression BindProperty(PropertyInfo resultProperty, Expression target, out Expression restrictions)
    {
        Debug.Assert(resultProperty.DeclaringType is not null);
        restrictions = Expression.TypeIs(target, resultProperty.DeclaringType);

        // reinterpret reference type without casting because it is protected by restriction
        target = Expression.Call(typeof(Unsafe), nameof(Unsafe.As), [resultProperty.DeclaringType], target);
        target = Expression.Property(target, resultProperty);
        return target.Type.IsValueType ? Expression.Convert(target, typeof(object)) : target;
    }

    private static Expression Bind(object targetValue, Expression target, LabelTarget returnLabel)
    {
        const string resultPropertyName = nameof(Task<Missing>.Result);
        const BindingFlags resultPropertyFlags = BindingFlags.Public | BindingFlags.Instance;
        
        var property = targetValue.GetType().GetProperty(resultPropertyName, resultPropertyFlags);
        Debug.Assert(property is not null);
        target = BindProperty(property, target, out var restrictions);

        target = Expression.Return(returnLabel, target);
        target = Expression.Condition(restrictions, target, Expression.Goto(UpdateLabel));
        return target;
    }

    public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel) => Bind(args[0], parameters[0], returnLabel);
}