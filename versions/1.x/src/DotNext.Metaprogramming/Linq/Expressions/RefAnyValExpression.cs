using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using Memory = Runtime.InteropServices.Memory;

    /// <summary>
    /// Represents expression that is equivalent to <c>__refvalue</c> C# undocumented keyword
    /// or <c>refanyval</c> IL instruction.
    /// </summary>
    public sealed class RefAnyValExpression : Expression
    {
        /// <summary>
        /// Initializes a new expression.
        /// </summary>
        /// <param name="typedRef">The variable of type <see cref="TypedReference"/>.</param>
        /// <param name="referenceType">The type of the managed reference.</param>
        /// <exception cref="ArgumentException"><paramref name="typedRef"/> is not of type <see cref="TypedReference"/>.</exception>
        public RefAnyValExpression(ParameterExpression typedRef, Type referenceType)
        {
            TypedReferenceVar = typedRef.Type == typeof(TypedReference) ? typedRef : throw new ArgumentException(ExceptionMessages.TypedReferenceExpected, nameof(typedRef));
            ReferenceType = referenceType.IsByRef ? referenceType.GetElementType() : referenceType;
        }

        /// <summary>
        /// Gets a variable that holds the value of type <see cref="TypedReference"/>.
        /// </summary>
        public ParameterExpression TypedReferenceVar { get; }

        /// <summary>
        /// Gets type of the managed reference.
        /// </summary>
        public Type ReferenceType { get; }

        /// <summary>
        /// Gets the type of this expression.
        /// </summary>
        public override Type Type => ReferenceType.MakeByRefType();

        /// <summary>
        /// Always returns <see langword="true"/> because
        /// this expression is <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce() => Call(typeof(Memory), nameof(Memory.AsRef), new[] { ReferenceType }, TypedReferenceVar);
    }
}