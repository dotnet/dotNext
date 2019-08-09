using Mono.Cecil;
using System.Diagnostics;
using System.Linq;

namespace DotNext.Runtime.CompilerServices
{
    internal static class ModuleDefinitionExtensions
    {
        private static readonly string DebuggableAttributeName = typeof(DebuggableAttribute).FullName;
        private static readonly string DebuggingModesName = DebuggableAttributeName + '/' + typeof(DebuggableAttribute.DebuggingModes).Name;

        internal static bool IsDebugBuild(this ModuleDefinition module)
        {
            var debuggableAttribute = module.Assembly.CustomAttributes.FirstOrDefault(i => i.AttributeType.FullName == DebuggableAttributeName)
                                      ?? module.CustomAttributes.FirstOrDefault(i => i.AttributeType.FullName == DebuggableAttributeName);

            if (debuggableAttribute == null)
                return false;

            var args = debuggableAttribute.ConstructorArguments;

            switch (args.Count)
            {
                case 1 when args[0].Type.FullName == DebuggingModesName && args[0].Value is int intValue:
                    return ((DebuggableAttribute.DebuggingModes)intValue & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;

                case 2 when args[0].Value is bool && args[1].Value is bool isJitOptimizerDisabled:
                    return isJitOptimizerDisabled;

                default:
                    return false;
            }
        }
    }
}
