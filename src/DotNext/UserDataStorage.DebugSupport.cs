using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

using Dictionary = Collections.Generic.Dictionary;

[DebuggerDisplay($"Source = {{{nameof(source)}}}")]
[DebuggerTypeProxy(typeof(DebugView))]
public partial struct UserDataStorage
{
    [ExcludeFromCodeCoverage]
    private readonly struct DebugView
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public readonly IReadOnlyDictionary<string, object> Entries;

        public DebugView(UserDataStorage storage) => Entries = storage.Capture();
    }

    /// <summary>
    /// Extracts a copy of all custom data in this storage.
    /// </summary>
    /// <remarks>
    /// This method is useful for debugging purposes to observe the data associated
    /// with arbitrary object.
    /// </remarks>
    /// <returns>The copy of all custom data.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IReadOnlyDictionary<string, object> Capture()
        => GetStorage()?.Dump() ?? ReadOnlyDictionary<string, object>.Empty;
}