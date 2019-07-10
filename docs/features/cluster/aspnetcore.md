Clustered ASP.NET Core Microservices
====
.NEXT provides fully-featured implementation of cluster computing infrastructure for microservices constructed on top of ASP.NET Core. This implementation consists of the following features:
* Point-to-point messaging between microservices organized through HTTP
* Consensus algorithm is Raft and all necessary communication for this algorithm is based on HTTP
* Replication according with Raft algorithm is fully supported. In-memory audit trail is used by default.

In this implementation, Web application treated as cluster node. The following example demonstrates how to turn ASP.NET Core application into cluster node:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class Startup : StartupBase
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration) => this.configuration = configuration;

    public override void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler();	//informs that processing pipeline should handle Raft-specific requests
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.BecomeClusterMember(configuration);	//registers all necessary services required for normal cluster node operation
    }
}

```

Raft algorithm requires dedicated HTTP endpoint for internal purposes. There are two possible ways to expose necessary endpoint:
* **Hosted Mode** exposes internal endpoint at different port because dedicated [Web Host](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.iwebhost) is used
* **Embedded Mode** exposes internal endpoint at the same port as underlying web application

These two modes are described in details below.



# Dependency Injection
Web application component can request the following service from ASP.NET Core DI container:
* [ICluster](../../api/DotNext.Net.Cluster.ICluster.yml)
* [IRaftCluster](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftCluster.yml) represents Raft-specific version of `ICluster` interface
* [IMessageBus](../../api/DotNext.Net.Cluster.Messaging.IMessageBus.yml) for point-to-point messaging between nodes
* [IExpandableCluster](../../api/DotNext.Net.Cluster.ICluster.yml) for tracking changes in cluster membership
* [IReplicationCluster&lt;ILogEntry&gt;](../../api/DotNext.Net.Cluster.Replication.IReplicationCluster.yml) to work with audit trail used for replication. [ILogEntry](../../api/DotNext.Net.Cluster.Consensus.Raft.ILogEntry.yml) is Raft-specific representation of the record in the audit trail.

# Configuration
The application should be configured properly to work as a cluster node. The following JSON represents example of configuration:
```json
{
	"partitioning" : false,
	"lowerElectionTimeout": 150,
	"upperElectionTimeout": 300,
	"members": ["http://localhost:3262", "http://localhost:3263", "http://localhost:3264"],
	"metadata":
	{
		"key": "value"
	},
	"allowedNetworks": ["127.0.0.0", "255.255.0.0/16", "2001:0db9::1/64"]
	"requestJournal":
	{
		"memoryLimit": 5,
		"expiration": "00:01:00",
		"pollingInterval" : "00:00:10"
	},
	"resourcePath": "/cluster-consensus/raft",
	"port": 3262
}
```

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| partitioning | No | false | `true` if partitioning supported. In this case, each cluster partition may have its own leader, i.e. it is possible to have more that one leader for external observer. However, single partition cannot have more than 1 leader. `false` if partitioning is not supported and only one partition with majority of nodes can have leader. Note that cluster may be totally unavailable even if there are operating members presented
| lowerElectionTimeout, upperElectionTimeout  | No | 150, 300 |  Defines range for election timeout which is picked randomly inside of it for each cluster member. If cluster node doesn't receive heartbeat from leader node during this timeout then it becomes a candidate and start a election. The recommended value for  _upperElectionTimeout_ is `2  X lowerElectionTimeout`
| members | Required | Yes | N/A | An array of all cluster nodes. This list must include local node. DNS name cannot be used as host name in URL except `localhost`. Only IP address is allowed |
| allowedNetworks | No | Empty list which means that all networks are allowed | List of networks with other nodes which a part of single cluster. This property can be used to restrict unathorized requests to the internal endpoint responsible for handling Raft messages |
| metadata | No | empty dictionary | A set of key/value pairs to be associated with cluster node. The metadata is queriable through `IClusterMember` interface |
| resourcePath | No | /cluster-consensus/raft | This configuration parameter is relevant for Embedded Mode only. It defines relative path to the endpoint responsible for handling internal Raft messages |
| port | No | 32999 | This configuration is relevant for Hosted Mode only. It defines the port number that the internal endpoint handler is listening to.
| requestJournal.memoryLimit | No | 10 | The maximum amount of memory (in MB) utilized by internal buffer used to track duplicate messages |
| requestJournal.expiration | No | 00:00:10 | The eviction time of the record containing unique request identifier |
| requestJournal.pollingTime | No | 00:01:00 | Gets the maximum time after which the buffer updates its memory statistics |



## Runtime Hook
IRaftClusterConfigurator

## HTTP Client Behavior
IHttpMessageHandlerFactory

# Hosted Mode

# Embedded Mode

/cluster-consensus/raft

# Redirection to Leader

# Messaging
IMessageHandler

# Replication
Raft algorithm requires additional persistent state in order to basic audit trail. This state is represented by [IPersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.IPersistentState.yml) interface. By default, it is implemented as in-memory storage which is suitable only for applications that doesn't have replicated state. If your application has it then implement this interface manually and use reliable storage such as disk and inject this implementation through `AuditTrail` property in [IRaftCluster](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftCluster.yml) interface. This injection should be done in user-defined implementation of [IRaftClusterConfigurator](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftClusterConfigurator.yml) interface registered as singleton service in ASP.NET Core application.

# Example