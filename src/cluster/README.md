.NEXT Raft Suite
====
.NEXT Raft Suite is a set of libraries for building clustered microservices:
* [DotNext.Net.Cluster](https://www.nuget.org/packages/DotNext.Net.Cluster/) contains cluster programming model, transport-agnostic implementation of Raft algorithm, TCP and UDP transport
* [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) is a concrete implementation of Raft algorithm on top of _DotNext.Net.Cluster_ library for building ASP.NET Core applications. The transport for Raft messages is HTTP 1.1 and HTTP/2. TLS is also supported.

The list of supported features:
* Network transport: TCP, UDP, HTTP 1.1, HTTP/2, HTTP/3
* High-performance, general-purpose [Persistent Write-Ahead Log](https://sakno.github.io/dotNext/features/cluster/wal.html) supporting log compaction
* Replication of log entries across cluster nodes
* Tight integration with ASP.NET Core framework
* Friendly to Docker/LXC/Windows containers, e.g. port mapping between the host and the container
* Everything is extensible
    * Custom write-ahead log
    * Custom network transport

Useful links:
* [Overview of Cluster Programming Model](https://sakno.github.io/dotNext/features/cluster/index.html)
* [Cluster Programming using Raft](https://sakno.github.io/dotNext/features/cluster/raft.html)
* [API Reference](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.html)