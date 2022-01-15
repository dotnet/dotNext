using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext;

using static Resources.ResourceManagerExtensions;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string EntrySetIsEmpty => (string)Resources.Get();

    internal static string LocalNodeNotLeader => (string)Resources.Get();

    internal static string InvalidEntryIndex(long index) => Resources.Get().Format(index);

    internal static string InvalidAppendIndex => (string)Resources.Get();

    internal static string SnapshotDetected => (string)Resources.Get();

    internal static string RangeTooBig => (string)Resources.Get();

    internal static string UnexpectedError => (string)Resources.Get();

    internal static string UnsupportedAddressFamily => (string)Resources.Get();

    internal static string DuplicateCorrelationId => (string)Resources.Get();

    internal static string UnexpectedUdpSenderBehavior => (string)Resources.Get();

    internal static string ExchangeCompleted => (string)Resources.Get();

    internal static string UnavailableMember => (string)Resources.Get();

    internal static string MissingPartition(long index) => Resources.Get().Format(index);

    internal static string UnknownCommand(int id) => Resources.Get().Format(id);

    internal static string MissingCommandId => (string)Resources.Get();

    internal static string MissingMessageName => (string)Resources.Get();

    internal static string LeaderIsUnavailable => (string)Resources.Get();

    internal static string UnknownRaftMessageType<T>(T messageType)
        where T : struct, Enum
        => Resources.Get().Format(messageType.ToString());

    internal static string PersistentStateBroken => (string)Resources.Get();
}