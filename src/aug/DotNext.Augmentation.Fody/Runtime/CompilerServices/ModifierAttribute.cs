﻿using Mono.Cecil;

namespace DotNext.Runtime.CompilerServices
{
    internal static class ModifierAttribute
    {
        private const string OptionalModifier = "DotNext.Runtime.CompilerServices.OptionalModifierAttribute";
        private const string RequiredModifier = "DotNext.Runtime.CompilerServices.RequiredModifierAttribute";

        internal static TypeReference GetModifierType(this CustomAttribute attribute, out bool required)
        {
            using (var io = System.IO.File.AppendText("C:\\Users\\r_sakno\\Weaver.txt"))
                io.WriteLine(attribute.AttributeType.FullName);
                switch (attribute.AttributeType.FullName)
            {
                case OptionalModifier:
                    required = false;
                    return attribute.ConstructorArguments[0].Value as TypeReference;
                case RequiredModifier:
                    required = true;
                    return attribute.ConstructorArguments[0].Value as TypeReference;
                default:
                    required = default;
                    return null;
            }
        }
    }
}
