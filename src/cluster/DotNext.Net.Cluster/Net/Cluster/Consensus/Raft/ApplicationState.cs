using System;
using System.Collections.Concurrent;
using System.IO;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents persistent state of distributed application.
    /// </summary>
    /// <remarks>
    /// This class is mandatory in order to support distributed services.
    /// </remarks>
    public partial class ApplicationState : PersistentState
    {
        public ApplicationState(DirectoryInfo path, int recordsPerPartition, Options? configuration = null)
            : base(path, recordsPerPartition, configuration)
        {
            lockState = new DirectoryInfo(Path.Combine(path.FullName, LockDirectoryName));
            if(!lockState.Exists)
                lockState.Create();
            waitNodes = new ConcurrentDictionary<string, WaitNode>(StringComparer.Ordinal);
        }
    }
}