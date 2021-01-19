using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    using static Collections.Generic.Sequence;
    using static Resources.ResourceManagerExtensions;

    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string AbstractDelegate => (string)Resources.Get();

        internal static string ObjectOfTypeExpected(object? obj, Type t)
            => Resources.Get().Format(obj, t.FullName);

        internal static string ReadOnlyField(string fieldName)
            => Resources.Get().Format(fieldName);

        internal static string NullFieldValue => (string)Resources.Get();

        internal static string PropertyWithoutGetter(string propertyName)
            => Resources.Get().Format(propertyName);

        internal static string PropertyWithoutSetter(string propertyName)
            => Resources.Get().Format(propertyName);

        internal static string ThisParamExpected => (string)Resources.Get();

        internal static string MissingOperator<TEnum>(TEnum op)
            where TEnum : struct, Enum
            => Resources.Get().Format(op);

        internal static string MissingAttribute(Type attribute, Type target)
            => Resources.Get().Format(attribute.FullName, target.FullName);

        internal static string MissingCtor(Type target, IEnumerable<Type> parameters)
            => Resources.Get().Format(target.FullName, parameters.ToString(","));

        internal static string MissingEvent(string eventName, Type handlerType, Type declaringType)
            => Resources.Get().Format(eventName, handlerType.FullName, declaringType.FullName);

        internal static string MissingField(string fieldName, Type fieldType, Type declaringType)
            => Resources.Get().Format(fieldName, fieldType.FullName, declaringType.FullName);

        internal static string MissingMethod(string methodName, IEnumerable<Type> parameters, Type returnType, Type declaringType)
            => Resources.Get().Format(methodName, parameters.ToString(","), returnType, declaringType);

        internal static string MissingProperty(string propertyName, Type propertyType, Type declaringType)
            => Resources.Get().Format(propertyName, propertyType.FullName, declaringType.FullName);

        internal static string ExtensionMethodExpected(MethodBase method)
            => Resources.Get().Format(method.Name);

        internal static string ConceptTypeInvalidAttribution<TAttribute>(Type conceptType)
            where TAttribute : Attribute
            => Resources.Get().Format(conceptType.FullName, typeof(TAttribute).FullName);

        internal static string StaticCtorDetected => (string)Resources.Get();

        internal static string ModuleMemberDetected(MemberInfo member)
            => Resources.Get().Format(member.Name);

        internal static string StaticFieldExpected => (string)Resources.Get();

        internal static string InstanceFieldExpected => (string)Resources.Get();

        internal static string AwaiterMustNotBeNull => (string)Resources.Get();
    }
}
