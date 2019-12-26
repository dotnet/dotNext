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
        }
    }
}