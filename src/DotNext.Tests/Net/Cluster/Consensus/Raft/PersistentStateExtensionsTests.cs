namespace DotNext.Net.Cluster.Consensus.Raft;

public sealed class PersistentStateExtensionsTests : Test
{
    // last entry is at index 3, term 2
    private static async ValueTask<IPersistentState> CreateAuditTrailAsync(CancellationToken token)
    {
        IPersistentState auditTrail = new ConsensusOnlyState();
        await auditTrail.AppendAsync(new EmptyLogEntry { Term = 1L }, token);
        await auditTrail.AppendAsync(new EmptyLogEntry { Term = 1L }, token);
        await auditTrail.AppendAsync(new EmptyLogEntry { Term = 2L }, token);

        Equal(3L, auditTrail.LastEntryIndex);
        Equal(2L, await auditTrail.GetTermAsync(auditTrail.LastEntryIndex, token));
        return auditTrail;
    }

    [Fact]
    public static async Task HigherTermShorterLogIsUpToDate()
    {
        var auditTrail = await CreateAuditTrailAsync(TestToken);

        True(await auditTrail.IsUpToDateAsync(2L, 3L, TestToken));
    }

    [Fact]
    public static async Task StaleCandidateIsRejected()
    {
        var auditTrail = await CreateAuditTrailAsync(TestToken);

        False(await auditTrail.IsUpToDateAsync(8L, 1L, TestToken));
    }

    [Theory]
    [InlineData(3L, 2L, true)]  // same term, same length
    [InlineData(4L, 2L, true)]  // same term, longer log
    [InlineData(2L, 2L, false)] // same term, shorter log
    [InlineData(3L, 3L, true)]  // higher term, same length
    [InlineData(2L, 3L, true)]  // higher term, shorter log
    [InlineData(100L, 1L, false)] // lower term, longer log
    [InlineData(0L, 0L, false)] // empty candidate vs non-empty log
    public static async Task UpToDateMatchesRaftLexicographicRule(long lastLogIndex, long lastLogTerm, bool expected)
    {
        var auditTrail = await CreateAuditTrailAsync(TestToken);

        Equal(expected, await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, TestToken));
    }

    [Fact]
    public static async Task EmptyLocalLog()
    {
        IPersistentState auditTrail = new ConsensusOnlyState();

        Equal(0L, auditTrail.LastEntryIndex);
        Equal(0L, await auditTrail.GetTermAsync(0L, TestToken));

        True(await auditTrail.IsUpToDateAsync(0L, 0L, TestToken));
        True(await auditTrail.IsUpToDateAsync(1L, 0L, TestToken));
        True(await auditTrail.IsUpToDateAsync(0L, 1L, TestToken));
        False(await auditTrail.IsUpToDateAsync(0L, -1L, TestToken));
    }
}
