using Fody;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Generic;

namespace DotNext.Runtime.CompilerServices
{
    public sealed class ModuleWeaver : BaseModuleWeaver
    {
        private static void ProcessModifier<TWeaver>(TWeaver weaver, Collection<CustomAttribute> attributes)
            where TWeaver : struct, IModifierWeaver
        {
            ICollection<CustomAttribute> attributesToRemove = new LinkedList<CustomAttribute>();
            foreach (var attr in attributes)
            {
                var modifierType = attr.GetModifierType(out var required);
                if (modifierType is null)
                    continue;
                attributesToRemove.Add(attr);
                weaver.AttachModifier(modifierType, required);
            }
            //remove fake attributes
            foreach (var attr in attributesToRemove)
                attributes.Remove(attr);
        }

        public override bool ShouldCleanReference => true;

        public override void Execute()
        {
            foreach (var type in ModuleDefinition.GetTypes())
            {
                BeforeFieldInitWeaver.Process(type);
                foreach (var field in type.Fields)
                    ProcessModifier(new FieldModifierWeaver(field), field.CustomAttributes);
                foreach (var method in type.Methods)
                {
                    if (!method.IsInternalCall && method.Body != null)
                        ValueDelegateWeaver.Process(method.Body, new Fody.TypeSystem(FindType, ModuleDefinition));
                    foreach (var param in method.Parameters)
                        ProcessModifier(new ParameterModifierWeaver(param), param.CustomAttributes);
                    ProcessModifier(new ReturnTypeModifierWeaver(method.MethodReturnType), method.MethodReturnType.CustomAttributes);
                }
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "netstandard";
            yield return "mscorlib";
            yield return "DotNext.dll";
        }
    }
}
