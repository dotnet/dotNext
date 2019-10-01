Raft for ASP.NET Core
====
Raft implementation for ASP.NET Core is read-to-production library that allows to build clustered microservices. There are two libraries:

* [DotNext.Net.Cluster](https://www.nuget.org/packages/DotNext.Net.Cluster/) contains cluster programming model and transport-agnostic implementation of Raft algorithm. The library also shipped with persistent [Write Ahead Log](https://sakno.github.io/dotNext/features/cluster/wal.html)
* [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) is a concrete implementation of Raft algorithm on top of _DotNext.Net.Cluster_ library for building ASP.NET Core applications. The transport for Raft messages in HTTP/S.

Other links:
* [Documentation](https://sakno.github.io/dotNext/features/cluster/index.html)
* [API Reference](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.html)