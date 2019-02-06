using System;
using static System.Runtime.ExceptionServices.ExceptionDispatchInfo;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        private static class ConceptHolder<C>
            where C: IConcept<T>, new()
        {
            internal static readonly C Value = new C();
        }

        /// <summary>
        /// Applies a concept represented by static class to type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="conceptType">A static type describing concept.</param>
        /// <exception cref="ConstraintViolationException">Type <typeparamref name="T"/> violates one of constraints specified by concept.</exception>
        public static void Concept(Type conceptType)
        {
            try
            {
                //run class constructor for concept type and its parents
                while(!(conceptType is null))
                {
                    RunClassConstructor(conceptType.TypeHandle);
                    conceptType = conceptType.BaseType;
                }
            } 
            catch(TypeInitializationException e)
            {
                if(e.InnerException is ConstraintViolationException constraintViolation)
                    Capture(constraintViolation).Throw();
                throw;
            }
        }

        /// <summary>
        /// Applies a concept to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="C">Type of concept.</typeparam>
        /// <returns>Concept instance.</returns>
        /// <exception cref="ConstraintViolationException">Type <typeparamref name="T"/> violates one of constraints specified by concept.</exception>
        public static C Concept<C>()
            where C: IConcept<T>, new()
        {
            Concept(typeof(C));
            return ConceptHolder<C>.Value;
        }
    }
}