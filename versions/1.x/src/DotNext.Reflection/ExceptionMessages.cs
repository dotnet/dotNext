using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string AbstractDelegate => Resources.GetString("AbstractDelegate");

        internal static string ObjectOfTypeExpected(object obj, Type t) => string.Format(Resources.GetString("ObjectOfTypeExpected"), obj, t.FullName);

        internal static string ReadOnlyField(string fieldName) => string.Format(Resources.GetString("ReadOnlyField"), fieldName);

        internal static string NullFieldValue => Resources.GetString("NullFieldValue");

        internal static string PropertyWithoutGetter(string propertyName) => string.Format(Resources.GetString("PropertyWithoutGetter"), propertyName);

        internal static string PropertyWithoutSetter(string propertyName) => string.Format(Resources.GetString("PropertyWithoutSetter"), propertyName);

        internal static string ThisParamExpected => Resources.GetString("ThisParamExpected");

        internal static string MissingOperator<E>(E op)
            where E : struct, Enum
            => string.Format(Resources.GetString("MissingOperator"), op);

        internal static string MissingAttribute(Type attribute, Type target) => string.Format(Resources.GetString("MissingAttribute"), attribute.FullName, target.FullName);

        internal static string MissingCtor(Type target, IEnumerable<Type> parameters) => string.Format(Resources.GetString("MissingCtor"), target.FullName, parameters.ToString(","));

        internal static string MissingEvent(string eventName, Type handlerType, Type declaringType) => string.Format(Resources.GetString("MissingEvent"), eventName, handlerType.FullName, declaringType.FullName);

        internal static string MissingField(string fieldName, Type fieldType, Type declaringType) => string.Format(Resources.GetString("MissingField"), fieldName, fieldType.FullName, declaringType.FullName);

        internal static string MissingMethod(string methodName, IEnumerable<Type> parameters, Type returnType, Type declaringType) => string.Format(Resources.GetString("MissingMethod"), methodName, parameters.ToString(","), returnType, declaringType);

        internal static string MissingProperty(string propertyName, Type propertyType, Type declaringType) => string.Format(Resources.GetString("MissingProperty"), propertyName, propertyType.FullName, declaringType.FullName);

        internal static string ExtensionMethodExpected(MethodBase method) => string.Format(Resources.GetString("ExtensionMethodExpected"), method.Name);

        internal static string ConceptTypeInvalidAttribution<A>(Type conceptType) where A : Attribute => string.Format(Resources.GetString("ConceptTypeInvalidAttribution"), conceptType.FullName, typeof(A).FullName);

        internal static string StaticCtorDetected => Resources.GetString("StaticCtorDetected");

        internal static string ModuleMemberDetected(MemberInfo member) => string.Format(Resources.GetString("ModuleMemberDetected"), member.Name);

        internal static string InvalidFieldType => Resources.GetString("InvalidFieldType");

        internal static string StaticFieldExpected => Resources.GetString("StaticFieldExpected");

        internal static string InstanceFieldExpected => Resources.GetString("InstanceFieldExpected");
    }
}
