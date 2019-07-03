using System;
using System.IO;
using DotNext.Net.Cluster.Messaging;
using Microsoft.Extensions.Configuration;

namespace RaftNode
{
    internal sealed class FileListener : FileSystemWatcher
    {
        internal const string MessageFile = "messageFile";

        private readonly IMessageBus messageBus;
        private readonly string fileName;

        public FileListener(IMessageBus messageBus, IConfiguration configuration)
        {
            NotifyFilter = NotifyFilters.LastWrite;
            this.messageBus = messageBus;
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

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && Equals(e.Name, fileName))
            {
                //read file content and send to leader
                var content = File.ReadAllText(e.FullPath);
                await messageBus.SendSignalToLeaderAsync(new TextMessageFromFile(content)).ConfigureAwait(false);
            }
        }
    }
}
