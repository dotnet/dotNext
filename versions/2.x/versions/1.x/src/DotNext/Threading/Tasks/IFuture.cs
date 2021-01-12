using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks
{
    /// <summary>
    /// Represents Future pattern.
    /// </summary>
    public interface IFuture : INotifyCompletion
    {
        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        bool IsCompleted { get; }
    }
}
