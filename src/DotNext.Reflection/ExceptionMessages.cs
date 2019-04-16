using System;
using System.Resources;
using System.Reflection;

namespace DotNext
{
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string AbstractDelegate => resourceManager.GetString("AbstractDelegate");

        internal static string ObjectOfTypeExpected(object obj, Type t) => string.Format(resourceManager.GetString("ObjectOfTypeExpected"), obj, t.FullName);

        internal static string ReadOnlyField(string fieldName) => string.Format(resourceManager.GetString("ReadOnlyField"), fieldName);

        internal static string NullFieldValue => resourceManager.GetString("NullFieldValue");

        internal static string PropertyWithoutGetter(string propertyName) => string.Format(resourceManager.GetString("PropertyWithoutGetter"), propertyName);

        internal static string PropertyWithoutSetter(string propertyName) => string.Format(resourceManager.GetString("PropertyWithoutSetter"), propertyName);

        internal static string ThisParamExpected => resourceManager.GetString("ThisParamExpected");

        internal static string MissingOperator<E>(E op)
            where E : struct, Enum
            => string.Format(resourceManager.GetString("MissingOperator"), op);

        internal static string MissingAttribute(Type attribute, Type target) => string.Format(resourceManager.GetString("MissingAttribute"), attribute.FullName, target.FullName);

        internal static string MissingCtor(Type target, Type[] parameters) => string.Format(resourceManager.GetString("MissingCtor"), target.FullName, parameters.ToString(","));

        internal static string MissingEvent(string eventName, Type handlerType, Type declaringType) => string.Format(resourceManager.GetString("MissingEvent"), eventName, handlerType.FullName, declaringType.FullName);

        internal static string MissingField(string fieldName, Type fieldType, Type declaringType) => string.Format(resourceManager.GetString("MissingField"), fieldName, fieldType.FullName, declaringType.FullName);

        internal static string MissingMethod(string methodName, Type[] parameters, Type returnType, Type declaringType) => string.Format(resourceManager.GetString("MissingMethod"), methodName, parameters.ToString(","), returnType, declaringType);

        internal static string MissingProperty(string propertyName, Type propertyType, Type declaringType) => string.Format(resourceManager.GetString("MissingProperty"), propertyName, propertyType.FullName, declaringType.FullName);

        internal static string ExtensionMethodExpected(MethodBase method) => string.Format(resourceManager.GetString("ExtensionMethodExpected"), method.Name);
    
        internal static string ConceptTypeInvalidAttribution<A>(Type conceptType) where A : Attribute => string.Format(resourceManager.GetString("ConceptTypeInvalidAttribution"), conceptType.FullName, typeof(A).FullName);
    }
}
