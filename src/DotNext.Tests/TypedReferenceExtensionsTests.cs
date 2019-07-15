using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotNext
{
    using static Reflection.Reflector;

    public sealed class TypedReferenceExtensionsTests : Assert
    {
        private Guid field;
        private string str;

        [Fact]
        public void FieldTypedReferenceValueType()
        {
            TypedReference reference = __makeref(field);
            ref Guid g = ref reference.AsRef<Guid>();
            Equal(Guid.Empty, g);
            g = Guid.NewGuid();
            Equal(field, g);
            True(Unsafe.AreSame(ref field, ref g));
        }

        [Fact]
        public void FieldTypedReferenceClass()
        {
            TypedReference reference = __makeref(str);
            ref string f = ref reference.AsRef<string>();
            Null(f);
            f = "Hello, world!";
            Equal(str, f);
            True(Unsafe.AreSame(ref str, ref f));
        }
    }
}
