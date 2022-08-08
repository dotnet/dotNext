.NEXT Cluster Programming Suite
====
.NEXT Cluster Programming Suite is a set of libraries for building clustered microservices:
* [DotNext.Net.Cluster](https://www.nuget.org/packages/DotNext.Net.Cluster/) contains cluster programming model, transport-agnostic implementation of Raft algorithm, TCP and UDP transport bindings for Raft, transport-agnostic implementation of HyParView membersip protocol for Gossip-based messaging
* [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) is a concrete implementation of Raft and HyParView algorithms on top of _DotNext.Net.Cluster_ library for building ASP.NET Core applications

# Raft
List of supported features:
* Network transport: TCP, UDP, HTTP 1.1, HTTP/2, HTTP/3, custom transport on top of [ASP.NET Core Connections](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections) abstraction
* TLS support: TCP, HTTP 1.1, HTTP/2, HTTP/3
* High-performance, general-purpose [Persistent Write-Ahead Log](https://dotnet.github.io/dotNext/features/cluster/wal.html) supporting log compaction
* Replication of log entries across cluster nodes
* Tight integration with ASP.NET Core framework
* Friendly to Docker/LXC/Windows containers
* Everything is extensible
    * Custom write-ahead log
    * Custom network transport
    * Cluster members discovery

Useful links:
* [Overview of Cluster Programming Model](https://dotnet.github.io/dotNext/features/cluster/index.html)
* [Cluster Programming using Raft](https://dotnet.github.io/dotNext/features/cluster/raft.html)
* [API Reference](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.html)

# HyParView
List of supported features:
* Network transport: HTTP 1.1, HTTP/2, HTTP/3
* TLS support: HTTP 1.1, HTTP/2, HTTP/3
* Tight integration with ASP.NET Core framework
* Broadcasting support

Useful links:
* [Gossip messaging and peer discovery using HyParView](https://dotnet.github.io/dotNext/features/cluster/gossip.html)
* [API Reference](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Discovery.HyParView.html)