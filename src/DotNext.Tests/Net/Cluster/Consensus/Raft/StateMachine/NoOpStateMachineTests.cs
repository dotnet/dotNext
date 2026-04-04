namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

[Collection(TestCollections.WriteAheadLog)]
public sealed class NoOpStateMachineTests : Test
{
    [Fact]
    public static async Task MakeSnapshot()
    {
        const int threshold = 4;
        var machine = IStateMachine.CreateNoOpStateMachine(threshold);

        Equal(0L, await machine.ApplyAsync(new LogEntry(0L, 0L), TestToken));
        Equal(1L, await machine.ApplyAsync(new LogEntry(0L, 1L), TestToken));
        Equal(2L, await machine.ApplyAsync(new LogEntry(0L, 2L), TestToken));
        Equal(3L, await machine.ApplyAsync(new LogEntry(0L, 3L), TestToken));
        Null(machine.Snapshot);
        
        Equal(4L, await machine.ApplyAsync(new LogEntry(0L, 4L), TestToken));
        Null(machine.Snapshot);
        
        Equal(5L, await machine.ApplyAsync(new LogEntry(0L, 5L), TestToken));
        Equal(4L, machine.Snapshot?.Index);
        
        Equal(6L, await machine.ApplyAsync(new LogEntry(0L, 6L), TestToken));
        Equal(4L, machine.Snapshot?.Index);

        Equal(7L, await machine.ApplyAsync(new LogEntry(0L, 7L), TestToken));
        Equal(8L, await machine.ApplyAsync(new LogEntry(0L, 8L), TestToken));
        Equal(4L, machine.Snapshot?.Index);
        
        Equal(9L, await machine.ApplyAsync(new LogEntry(0L, 9L), TestToken));
        Equal(8L, machine.Snapshot?.Index);
    }

    [Fact]
    public static void InitializeWithIndex()
    {
        var machine = new NoOpStateMachine(snapshotDepth: 4L);
        machine.SetLastCommittedIndex(4L);
        Null(machine.As<IStateMachine>().Snapshot);
        
        machine.SetLastCommittedIndex(5L);
        Equal(4L, machine.As<IStateMachine>().Snapshot?.Index);
    }
}