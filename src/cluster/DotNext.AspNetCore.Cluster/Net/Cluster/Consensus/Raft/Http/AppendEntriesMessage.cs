using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using ContentDispositionHeaderValue = System.Net.Http.Headers.ContentDispositionHeaderValue;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Collections.Generic;

    internal sealed class AppendEntriesMessage : RaftHttpBooleanMessage
    {
        internal const string MessageType = "AppendEntries";
        private const string MultipartBoundary = "BOUNDARY";
        private const string ClusterMembersSection = "__members__";
        private const string DispositionType = "inline";

        private sealed class SerializedCustomPayload : HttpContent
        {
            private readonly IMessage message;

            internal SerializedCustomPayload(IMessage message)
            {
                this.message = message;
                Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
                Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionType)
                    {Name = message.Name, Size = message.Length};
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => message.CopyToAsync(stream);

            protected override bool TryComputeLength(out long length) => message.Length.TryGet(out length);
        }

        private sealed class DeserializedCustomPayload : MemoryStream, IMessage
        {
            private static int GetCapacity(long? size)
            {
                if (size is null)
                    return 2048;
                else if (size <= int.MaxValue)
                    return (int) size.Value;
                else
                    return int.MaxValue;
            }

            internal DeserializedCustomPayload(string name, string contentType, long? size)
                : base(GetCapacity(size))
            {
                Name = name;
                Type = new ContentType(contentType);
            }

            public string Name { get; }
            long? IMessage.Length => Length;

            public ContentType Type { get; }
        }

        public sealed class MemberInfo : IClusterMemberIdentity
        {
            public MemberInfo()
            {

            }

            public MemberInfo(IClusterMember member)
                : this(member, member.Endpoint)
            {
            }

            public MemberInfo(IClusterMemberIdentity member, IPEndPoint endpoint)
            {
                Id = member.Id;
                Name = member.Name;
                Address = endpoint.Address.ToString();
                Port = endpoint.Port;
            }

            [DataMember(IsRequired = true, Name = "Id")]
            public Guid Id { get; set; }
            
            [DataMember(IsRequired = true, Name = "Name")]
            public string Name { get; set; }

            [DataMember(IsRequired = true, Name = "Address")]
            public string Address { get; set; }

            [DataMember(IsRequired = true, Name = "Port")]
            public int Port { get; set; }

            [IgnoreDataMember]
            public IPEndPoint EndPoint => new IPEndPoint(IPAddress.Parse(Address), Port);
        }
        
        [CollectionDataContract(ItemName = "Member", Name = "ClusterMembers")]
        public sealed class ClusterMemberCollection : IEnumerable<MemberInfo>
        {
            private readonly IDictionary<IPEndPoint, IClusterMemberIdentity> members =
                new Dictionary<IPEndPoint, IClusterMemberIdentity>();

            public void Add(MemberInfo member)
            {
                members[member.EndPoint] = member;
            }

            public IEnumerator<MemberInfo> GetEnumerator()
            {
                foreach (var (endpoint, identity) in members)
                    yield return identity is MemberInfo info ? info : new MemberInfo(identity, endpoint);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal AppendEntriesMessage(IRaftLocalMember sender)
            : base(MessageType, sender)
        {
            Members = new ClusterMemberCollection();
        }

        private AppendEntriesMessage(HttpRequest request)
            : base(request)
        {
            
        }

        private static DataContractJsonSerializer CreateSerializer()
            => new DataContractJsonSerializer(typeof(ClusterMemberCollection));

        private void ParseClusterMembers(MultipartSection section)
            => Members = (ClusterMemberCollection) CreateSerializer().ReadObject(section.Body);

        private void AddClusterMembers(MultipartContent multipart)
        {
            var ms = new MemoryStream(2048);
            CreateSerializer().WriteObject(ms, Members);
            ms.Position = 0;
            var section = new StreamContent(ms);
            section.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
            section.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                {Name = ClusterMembersSection, Size = ms.Length};
            multipart.Add(section);
        }

        private void AddCustomPayload(MultipartContent multipart)
        {
            if(CustomPayload is null)
                return;
            multipart.Add(new SerializedCustomPayload(CustomPayload));
        }

        private Task ParseCustomPayload(MultipartSection section, ContentDispositionHeaderValue disposition)
        {
            var message = new DeserializedCustomPayload(disposition.Name, section.ContentType, disposition.Size);
            CustomPayload = message;
            return section.Body.CopyToAsync(message);
        }

        internal static async Task<AppendEntriesMessage> Parse(HttpRequest request, CancellationToken token)
        {
            var message = new AppendEntriesMessage(request);
            var reader = new MultipartReader(MultipartBoundary, request.Body);
            MultipartSection section;
            while ((section = await reader.ReadNextSectionAsync(token).ConfigureAwait(false)) != null)
            {
                var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
                switch (disposition.Name)
                {
                    case ClusterMembersSection:
                        message.ParseClusterMembers(section);
                        continue;
                    default:
                        await message.ParseCustomPayload(section, disposition).ConfigureAwait(false);
                        continue;
                }
            }

            return message;
        }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            var content = new MultipartContent("mixed", MultipartBoundary);
            AddClusterMembers(content);
            AddCustomPayload(content);
            request.Content = content;
        }

        internal ClusterMemberCollection Members { get; private set; }

        internal IMessage CustomPayload { get; set; }
    }
}