using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Replication;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static DotNext.Collections.Generic.List;

namespace RaftNode
{

    internal sealed class FileListener : FileSystemWatcher
    {
        internal const string MessageFile = "messageFile";

        private readonly string fileName;
        private readonly IRaftCluster cluster;

        public FileListener(IRaftCluster cluster, IConfiguration configuration)
        {
            this.cluster = cluster;
            NotifyFilter = NotifyFilters.LastWrite;
            fileName = configuration[MessageFile];
            Changed += OnChanged;
            if (File.Exists(fileName))
            {
                var file = new FileInfo(fileName);
                Path = file.DirectoryName;
                fileName = file.Name;
                EnableRaisingEvents = true;
            }
        }

        private ValueTask<IReadOnlyList<IRaftLogEntry>> WriteMessage(string content)
        {
            var entry = new TextMessageFromFile(content) { Term = cluster.Term };
            return new ValueTask<IReadOnlyList<IRaftLogEntry>>(Singleton(entry));
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            var isRemoteLeader = cluster.Leader?.IsRemote ?? true;
            if (e.ChangeType == WatcherChangeTypes.Changed && Equals(e.Name, fileName) && !isRemoteLeader)
            {
                //read file content and commit it
                var content = File.ReadAllText(e.FullPath);
                Console.WriteLine($"Committing message '{content}'");
                await cluster.WriteAsync(WriteMessage, content, WriteConcern.LeaderOnly);
            }
        }
    }
}
