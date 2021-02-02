Path MTU Discovery
====
If you are not aware about path MTU discovery please read [this](https://en.wikipedia.org/wiki/Path_MTU_Discovery) article first.

Detection of correct MTU size usually is not a problem for connection-oriented network protocols because this ability is supported out-of-the-box. Good example is Transmission Control Protocol (TCP). However, connectionless protocols are not aware about allowed MTU size in the route between two endpoints. .NEXT library provides [MtuDiscovery](xref:DotNext.Net.NetworkInformation.MtuDiscovery) class for discovery of allowed MTU size.

The following example demonstrates how to find MTU size between local machine and Internet address _1.1.1.1_:
```csharp
using DotNext.Net.NetworkInformation;
using System.Net;

using var discovery = new MtuDiscovery();
int? mtuSize = discovery.Discover(IPAddress.Parse("1.1.1.1"), 2000, new MtuDiscoveryOptions());
```

Asynchronous version in the form of `DiscoveryAsync` method is also supported.

The returned value may be **null** if MTU cannot be discovered due to timeout or unreachable remote endpoint. Otherwise, it represents maximum amount of IP packet payload that can be placed without IP packets fragmentation. The number doesn't include size of IP packet headers.
