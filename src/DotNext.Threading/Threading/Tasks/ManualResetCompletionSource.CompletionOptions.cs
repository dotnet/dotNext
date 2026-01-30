using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
    /// <summary>
    /// Represents completion options.
    /// </summary>
    /// <seealso cref="ExpectedToken"/>
    /// <seealso cref="CustomCompletionData"/>
    /// <seealso cref="ExpectedTokenAndCustomData"/>
    public interface ICompletionOptions
    {
        internal bool BeginCompletion(ManualResetCompletionSource source);

        internal bool EndCompletion(ManualResetCompletionSource source);
    }
    
    /// <summary>
    /// Represents the expected version of <see cref="ValueTaskCompletionSource"/> or <see cref="ValueTaskCompletionSource{T}"/>
    /// instance previously obtained with <see cref="Reset()"/> method.
    /// </summary>
    /// <param name="token">The expected token.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct ExpectedToken(short token) : ICompletionOptions
    {
        bool ICompletionOptions.BeginCompletion(ManualResetCompletionSource source)
            => source.BeginCompletion(token);

        bool ICompletionOptions.EndCompletion(ManualResetCompletionSource source)
            => source.EndCompletion();
    }
    
    /// <summary>
    /// Represents an arbitrary object to be passed to <see cref="CompletionData"/> when completed.
    /// </summary>
    /// <param name="userData">The user data object.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct CustomCompletionData(object? userData) : ICompletionOptions
    {
        bool ICompletionOptions.BeginCompletion(ManualResetCompletionSource source)
            => source.BeginCompletion();

        bool ICompletionOptions.EndCompletion(ManualResetCompletionSource source)
        {
            source.CompletionData = userData;
            return source.EndCompletion();
        }
    }

    /// <summary>
    /// Represents a combination of <see cref="ExpectedToken"/> and <see cref="CustomCompletionData"/> completion options.
    /// </summary>
    /// <param name="token">The expected token.</param>
    /// <param name="userData">The user data object.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct ExpectedTokenAndCustomData(short token, object? userData) : ICompletionOptions
    {
        bool ICompletionOptions.BeginCompletion(ManualResetCompletionSource source)
            => source.BeginCompletion(token);

        bool ICompletionOptions.EndCompletion(ManualResetCompletionSource source)
        {
            source.CompletionData = userData;
            return source.EndCompletion();
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct ExpectedSourceTokenAndSentinel(short token) : ICompletionOptions
    {
        bool ICompletionOptions.BeginCompletion(ManualResetCompletionSource source)
            => source.BeginCompletion(token);

        bool ICompletionOptions.EndCompletion(ManualResetCompletionSource source)
        {
            source.CompletionData = Sentinel.Instance;
            return source.EndCompletion();
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private protected readonly ref struct DefaultOptions : ICompletionOptions
    {
        bool ICompletionOptions.BeginCompletion(ManualResetCompletionSource source)
            => source.BeginCompletion();

        bool ICompletionOptions.EndCompletion(ManualResetCompletionSource source)
            => source.EndCompletion();
    }
}