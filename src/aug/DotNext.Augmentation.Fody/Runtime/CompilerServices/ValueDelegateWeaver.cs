using Mono.Cecil;
using Mono.Cecil.Cil;
using static System.Reflection.ConstructorInfo;

namespace DotNext.Runtime.CompilerServices
{
    internal static class ValueDelegateWeaver
    {
        private const string Namespace = "DotNext";
        private const string ValueActionType = "ValueAction";
        private const string ValueFuncType = "ValueFunc";

        private static void ReplaceValueDelegateConstruction(ILProcessor processor, TypeReference delegateType, Instruction instruction)
        {
            /*
             * ldnull
             * ldftn <static method>
             * newobj <DelegateType>::.ctor(object target, IntPtr method)
             * ldc_i4_0
             * newobj ValueDelegate::.ctor(<DelegateType>, bool)
             */
            //ldc_i4_0
            var loadFalse = instruction.Previous;
            if (loadFalse is null || loadFalse.OpCode.Code != Code.Ldc_I4_0)
                return;
            //newobj
            var newDelegate = loadFalse.Previous;
            if (newDelegate is null || newDelegate.OpCode.Code != Code.Newobj)
                return;
            //ldftn
            var ldftn = newDelegate.Previous;
            if (ldftn is null || ldftn.OpCode.Code != Code.Ldftn)
                return;
            //ldnull
            var loadNull = ldftn.Previous;
            if (loadNull is null || loadNull.OpCode.Code != Code.Ldnull)
                return;
            //extract method from ldftn
            if (!(ldftn.Operand is MethodReference methodRef) || methodRef.HasThis || methodRef.ExplicitThis)
                return;
            //remove all redundant instructions
            processor.Remove(loadNull);
            processor.Remove(newDelegate);
            processor.Remove(loadFalse);
            //replace ValueDelegate constructor with specialized version
            foreach (var ctor in delegateType.Resolve().Methods)
                if (ctor.Name == ConstructorName && ctor.Parameters.Count == 1 && ctor.Parameters[0].ParameterType.IsRequiredModifier)
                {
                    processor.Replace(instruction, Instruction.Create(instruction.OpCode, processor.Body.Method.Module.ImportReference(ctor)));
                    break;
                }
        }

        internal static void Process(MethodBody body)
        {
            for (var instruction = body.Instructions[0]; instruction != null; instruction = instruction.Next)
                if (instruction.OpCode.FlowControl == FlowControl.Call && instruction.Operand is MethodReference methodRef && methodRef.DeclaringType.Namespace == Namespace && methodRef.DeclaringType.IsValueType && methodRef.Name == ConstructorName && methodRef.Parameters.Count == 2 && (methodRef.DeclaringType.Name.StartsWith(ValueFuncType) || methodRef.DeclaringType.Name.StartsWith(ValueActionType)))
                    ReplaceValueDelegateConstruction(body.GetILProcessor(), methodRef.DeclaringType, instruction);
                    
        }
    }
}
