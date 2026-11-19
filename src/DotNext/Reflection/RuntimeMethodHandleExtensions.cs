using System.Reflection;

namespace DotNext.Reflection;

using static Runtime.CompilerServices.AdvancedHelpers;

internal static class RuntimeMethodHandleExtensions
{
    extension(RuntimeMethodHandle method)
    {
        public TDelegate? CreateDelegate<TDeclaringType, TDelegate>()
            where TDeclaringType : class
            where TDelegate : MulticastDelegate
            => (MethodBase.GetMethodFromHandle(method, get_TypeId<TDeclaringType>()) as MethodInfo)
                ?.CreateDelegate<TDelegate>();
    }
}