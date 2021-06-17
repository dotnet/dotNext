namespace DotNext.Net.Cluster.Messaging
{
    [Message("Add", Formatter = typeof(MessageFormatter))]
    public sealed class AddMessage
    {
        internal const int Size = sizeof(int) + sizeof(int);

        public int X { get; set; }
        public int Y { get; set; }

        public int Execute() => X + Y;
    }
}