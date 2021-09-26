.NEXT Cluster Development Suite
====
.NEXT Cluster Development Suite is a set of libraries for building clustered microservices:
* [DotNext.Net.Cluster](https://www.nuget.org/packages/DotNext.Net.Cluster/) contains cluster programming model, transport-agnostic implementation of Raft and HyParView algorithms, TCP and UDP transport bindings for Raft
* [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) is a concrete implementation of Raft and HyParView algorithms on top of _DotNext.Net.Cluster_ library for building ASP.NET Core applications.

The list of supported features:
* Raft
    * Network transport: TCP, UDP, HTTP 1.1, HTTP/2, HTTP/3
    * TLS support: TCP, HTTP 1.1, HTTP/2, HTTP/3
    * Replication of log entries across cluster nodes
    * Tight integration with ASP.NET Core framework
    * Raft-native cluster configuration management compatible with any hosting environment: Docker/CRI containers, Kubernetes, virtual machines, cloud
    * Everything is extensible
        * Custom write-ahead log
        * Custom network transport
* HyParView
    * Network transport: HTTP 1.1, HTTP/2, HTTP/3
    * TLS support: HTTP 1.1, HTTP/2, HTTP/3
* High-performance, general-purpose [Persistent Write-Ahead Log](https://dotnet.github.io/dotNext/features/cluster/wal.html) supporting log compaction
    * Background, sequential or parallel log compation
    * Incremental or inline snapshot building
    * Smart caching of log entries

Useful links:
* [Overview of Cluster Programming Model](https://dotnet.github.io/dotNext/features/cluster/index.html)
* [Cluster Programming using Raft](https://dotnet.github.io/dotNext/features/cluster/raft.html)
* [API Reference](https://www.fuget.org/packages/DotNext.Net.Cluster/latest/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft)