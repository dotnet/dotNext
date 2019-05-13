Object Pool
====

[ConcurrentObjectPool](../../api/DotNext.Threading.ConcurrentObjectPool-1.yml) is a way to share thread-unsafe objects between multiple threads in thread-safe manner. It allows to reause already initialized objects rather than allocating and destroying them on demand.

It is reasonable to use the pool for performance when the instantiation of the object is too costly and each objets holds reference to some external expensive resource such as network connection. An object lifetime management is covered by Object Pool implementation and very similar to Last Recently Used policy. The object that is least used will be disposed automatically.

Capacity of the pool indicates how many objects can be provided to the concurrent threads. The pool may blocks the caller thread until another thread returns an object to the pool.

> [!WARNING]
> It is not recommended to use object pool in situations when underlying objects only use memory and hold no external resources. 

Selection of the first available object from the pool depends on the chosen algorithm: Round-robin and Shortest Job First.

Once the object retrieved from the pool it is bounded to the caller exclusively until it is released. This process called **renting**. 

```csharp
using DotNext.Threading;
using System.Data;

ConcurrentObjectPool<IDbConnection> pool = ...;

using(var rental = pool.Rent())
{
    var connection = rental.Resource;   //Resource property returns IDbConnection object from the pool
    var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM Customers";
    command.Prepare();
    command.ExecuteReader();
}
```

Rented object is safely accessible inside of _using_ block. If you try to access the object without renting then behavior of object pool is unpredictable. 

When object is rented it is possible to replace object with a new instance except **null** value. It is useful if internal state of the object is broken (for instance, network connection unexpectedly closed or crashed). You can do this using setter of `Resource` property.

```csharp
using DotNext.Threading;
using System.Data;

ConcurrentObjectPool<IDbConnection> pool = ...;

using(var rental = pool.Rent())
{
    var connection = rental.Resource;   //Resource property returns IDbConnection object from the pool
    if(IsBroken(connection))
        connection = rental.Resource = RestoreConnection(connection);
}
```

Method `Rent()` is synchronous and may block the thread for some time. If waiting time is significant then you should increase the capacity of the object pool passed as constructor parameter.

> [!NOTE]
> The capacity cannot be changed for created object pool.

Typical use of object pool:
* Organize connection pool to share multiple database connections between application threads
* Organize pool of logical [channels](https://rabbitmq.github.io/rabbitmq-dotnet-client/api/RabbitMQ.Client.IModel.html) for the single RabbitMQ connection
* Reusing TCP connections

# Round-robin
This strategy shares objects in the pool between all requested threads in circular order. To use this strategy, all objects should be created and initialized before they are pooled. These objects will not be destroyed automatically by object pool.

```csharp
using DotNext.Threading;
using System.Data;
using System.Data.SqlClient;

//create objects for the pool
var connections = new IDbConnection[10];
for(var i = 0; i < connections.Length; i++)
    connections[i] = new SqlConnection(@"Server=(localdb)\V11.0");

var pool = new ConcurrentObjectPool<IDbConnection>(connections);
```

The capacity of the pool is determined implicitly from the size of the collection of objects passed into constructor.

It is recommended to use this strategy if workload is constant over time or unpredictable.

Read more about this strategy [here](https://en.wikipedia.org/wiki/Round-robin_scheduling).

# Shortest Job First
This strategy differs from round-robin in the following aspects:
1. Objects are creating lazily.
1. Objects are destroying automatically by the pool if they are not in use for a long period of time.
1. The most recently returned object to the pool will pulled by waiting thread, in contrast to cirtucal-based access.

You should provide factory as a constructor parameter that will be used by the pool for instantiating objects during pool lifetime.

```csharp
using DotNext.Threading;
using System.Data;
using System.Data.SqlClient;

var pool = new ConcurrentObjectPool<IDbConnection>(10, () => new SqlConnection(@"Server=(localdb)\V11.0"));
```

The capacity is determined explicitly as constructor parameter.

It is recommended to use this strategy if workload is variable but predictable and keeping unused objects in the pool is too costly.

Read more about this strategy [here](https://en.wikipedia.org/wiki/Shortest_job_next).

# Diagnostics
The main question is "how to identify the necessary capacity of the pool?". There is no universal answer. Moreover, it is not possible to identify it a priori. As usual, it it trade-off between number of expensive objects and real need of the application. Profiling is a good way to find the right way. `ConcurrentObjectPool` provides special property `WaitCount` that represents a number of threads waiting for the available objects if all objects are retrieved by another threads. The normal behavior when this property is in range _[0, capacity/2]_ and doesn't grow in time. Growing of this property in time is an evidence of resource starvation. In this case just increase the capacity of the pool.