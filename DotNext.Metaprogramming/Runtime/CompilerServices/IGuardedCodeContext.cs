using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal interface IGuardedCodeContext
    {
        LabelTarget FaultLabel { get; }
    }
}