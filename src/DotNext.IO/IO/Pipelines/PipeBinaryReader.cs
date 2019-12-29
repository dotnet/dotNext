using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DotNext.IO.Pipelines
{
    using DecodingContext = Text.DecodingContext;

    [StructLayout(LayoutKind.Auto)]
    internal struct PipeBinaryReader : IAsyncBinaryReader
    {
        private readonly PipeReader reader;

        internal PipeBinaryReader(PipeReader reader)
            => this.reader = reader;
        
        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => reader.ReadAsync<T>(token);
        
        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => reader.ReadAsync(output, token);
        
        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => reader.ReadStringAsync(length, context, token);
        
        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => reader.ReadStringAsync(lengthFormat, context, token);
    }
}