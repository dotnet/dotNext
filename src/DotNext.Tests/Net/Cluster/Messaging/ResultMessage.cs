namespace DotNext.Net.Cluster.Messaging
{
    [Message(Name, Formatter = typeof(MessageFormatter))]
    public sealed class ResultMessage
    {
        internal const string Name = "Result";
        internal const int Size = sizeof(int);

        public int Result { get; set; }

        public static implicit operator ResultMessage(int value) => new() { Result = value };
    }
}