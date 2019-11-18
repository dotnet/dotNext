Persistent Channel
====
[System.Threading.Channels](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels) allows to organize data exchange between producer and consumer using concept of **channel**. The standard implementation provides in-memory bounded and unbounded channels. Memory-based organization of storage for messages may be problematic in situation when producer has predictable and constant speed of generated messages but consumer speed may vary and depends on external factors. It leads to accumulation of messages in the channel and may cause _OutOfMemoryException_. Another disadvantage of in-memory channel is an inability to recover messages after crashes.

[PersistentChannel](https://sakno.github.io/dotNext/api/DotNext.Threading.Channels.PersistentChannel-2.html) provides reliable and persistent unbounded channel that can be recovered after app crash and not limited by RAM. However, it is more slower than in-memory channel. 
