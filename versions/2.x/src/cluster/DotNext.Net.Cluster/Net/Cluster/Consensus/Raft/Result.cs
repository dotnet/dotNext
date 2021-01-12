using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents RPC response.
    /// </summary>
    /// <typeparam name="T">The type of RPC response.</typeparam>
    [SuppressMessage("Design", "CA1051", Justification = "Structure represeting DTO-like object")]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Result<T>
    {
        /// <summary>
        /// Gets term of the remote member.
        /// </summary>
        public readonly long Term;

        /// <summary>
        /// Gets RPC response.
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// Initializes a new result.
        /// </summary>
        /// <param name="term">The term provided by remote node.</param>
        /// <param name="value">The value returned by remote node.</param>
        public Result(long term, T value)
        {
            Term = term;
            Value = value;
        }

        internal Result<TOther> SetValue<TOther>(TOther value) => new Result<TOther>(Term, value);
    }
}