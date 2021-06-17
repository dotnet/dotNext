namespace DotNext.Net.Cluster.Messaging
{
    [Message("Result", Formatter = typeof(MessageFormatter))]
    public sealed class ResultMessage
    {
        internal const int Size = sizeof(int);

        public int Result { get; set; }
    }
}