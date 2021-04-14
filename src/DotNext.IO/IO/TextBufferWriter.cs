using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.IO
{
    using Buffers;

    internal abstract unsafe class TextBufferWriter<T, TWriter> : TextWriter
        where T : struct, IEquatable<T>, IConvertible, IComparable<T>
        where TWriter : class, IBufferWriter<T>
    {
        private readonly delegate*<TWriter, ReadOnlySpan<T>, void> writeImpl;
        private protected readonly TWriter writer;
        private readonly Action<TWriter>? flush;
        private readonly Func<TWriter, CancellationToken, Task>? flushAsync;

        private protected TextBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
            : base(provider ?? InvariantCulture)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            writeImpl = writer is IReadOnlySpanConsumer<T> ?
                &DirectWrite :
                &BuffersExtensions.Write<T>;

            this.writer = writer;
            this.flush = flush;
            this.flushAsync = flushAsync;

            static void DirectWrite(TWriter output, ReadOnlySpan<T> input)
            {
                Debug.Assert(output is IReadOnlySpanConsumer<T>);
                Unsafe.As<IReadOnlySpanConsumer<T>>(output).Invoke(input);
            }
        }

        private protected void WriteCore(ReadOnlySpan<T> data) => writeImpl(writer, data);

        public sealed override void Flush()
        {
            if (flush is null)
            {
                if (flushAsync is not null)
                    flushAsync(writer, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                flush(writer);
            }
        }

        public sealed override Task FlushAsync()
        {
            if (flushAsync is null)
            {
                return flush is null ?
                    Task.CompletedTask
                    : Task.Factory.StartNew(() => flush(writer), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
            }
            else
            {
                return flushAsync(writer, CancellationToken.None);
            }
        }
    }
}