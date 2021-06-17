namespace DotNext.Net.Cluster.Messaging
{
    [Message(Name, Formatter = typeof(MessageFormatter))]
    public sealed class SubtractMessage
    {
        internal const string Name = "Subtract";
        internal const int Size = sizeof(int) + sizeof(int);

        public int X { get; set; }
        public int Y { get; set; }

        public int Execute() => X - Y;
    }
}