using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    using Buffers;

    internal sealed unsafe class CharBufferWriter<TWriter> : TextBufferWriter<char, TWriter>
        where TWriter : class, IBufferWriter<char>
    {
        internal CharBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
            : base(writer, provider, flush, flushAsync)
        {
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(ReadOnlySpan<char> buffer) => WriteCore(buffer);

        public override void WriteLine() => Write(new ReadOnlySpan<char>(CoreNewLine));

        public override Task WriteLineAsync()
        {
            var result = Task.CompletedTask;
            try
            {
                WriteLine();
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override void Write(char value) => Write(CreateReadOnlySpan(ref value, 1));

        public override Task WriteLineAsync(char value)
        {
            var result = Task.CompletedTask;
            try
            {
                Write(value);
                WriteLine();
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override void Write(bool value) => Write(value ? bool.TrueString : bool.FalseString);

        public override void Write(char[] buffer, int index, int count)
            => Write(new ReadOnlySpan<char>(buffer, index, count));

        public override void Write(char[]? buffer) => Write(new ReadOnlySpan<char>(buffer));

        public override void Write(string? value) => Write(value.AsSpan());

        public override Task WriteLineAsync(string? value)
        {
            var result = Task.CompletedTask;
            try
            {
                Write(value);
                WriteLine();
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override void Write(decimal value)
            => writer.WriteDecimal(value, string.Empty, FormatProvider);

        public override void Write(double value)
            => writer.WriteDouble(value, string.Empty, FormatProvider);

        public override void Write(float value)
            => writer.WriteSingle(value, string.Empty, FormatProvider);

        public override void Write(int value)
            => writer.WriteInt32(value, string.Empty, FormatProvider);

        public override void Write(long value)
            => writer.WriteInt64(value, string.Empty, FormatProvider);

        public override void Write(uint value)
            => writer.WriteUInt32(value, string.Empty, FormatProvider);

        public override void Write(ulong value)
            => writer.WriteUInt64(value, string.Empty, FormatProvider);

        public override void Write(object? value)
        {
            switch (value)
            {
                case null:
                    break;
                case byte v:
                    Write(v);
                    break;
                case sbyte v:
                    Write(v);
                    break;
                case short v:
                    Write(v);
                    break;
                case ushort v:
                    Write(v);
                    break;
                case int v:
                    Write(v);
                    break;
                case uint v:
                    Write(v);
                    break;
                case long v:
                    Write(v);
                    break;
                case ulong v:
                    Write(v);
                    break;
                case decimal v:
                    Write(v);
                    break;
                case float v:
                    Write(v);
                    break;
                case double v:
                    Write(v);
                    break;
                case DateTime v:
                    writer.WriteDateTime(v, string.Empty, FormatProvider);
                    break;
                case DateTimeOffset v:
                    writer.WriteDateTimeOffset(v, string.Empty, FormatProvider);
                    break;
                case TimeSpan v:
                    writer.WriteTimeSpan(v, string.Empty, FormatProvider);
                    break;
                case IFormattable formattable:
                    Write(formattable.ToString(null, FormatProvider));
                    break;
                default:
                    Write(value.ToString());
                    break;
            }
        }

        public override void WriteLine(object? value)
        {
            Write(value);
            WriteLine();
        }

        public override void WriteLine(ReadOnlySpan<char> buffer)
        {
            Write(buffer);
            WriteLine();
        }

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    Write(buffer.Span);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        public override Task WriteAsync(char value)
        {
            var result = Task.CompletedTask;
            try
            {
                Write(value);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override Task WriteAsync(string? value)
        {
            var result = Task.CompletedTask;
            try
            {
                Write(value);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
            => WriteAsync(buffer.AsMemory(index, count), CancellationToken.None);

        public override Task WriteLineAsync(char[] buffer, int index, int count)
        {
            var result = Task.CompletedTask;
            try
            {
                Write(buffer, index, count);
                WriteLine();
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    WriteLine(buffer.Span);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

#if !NETSTANDARD2_1
        public override void Write(StringBuilder? sb)
        {
            if (sb.IsNullOrEmpty())
                return;

            foreach (var chunk in sb.GetChunks())
                Write(chunk.Span);
        }

        public override void WriteLine(StringBuilder? sb)
        {
            Write(sb);
            WriteLine();
        }

        public override Task WriteAsync(StringBuilder? value, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    Write(value);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        public override Task WriteLineAsync(StringBuilder? value, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    WriteLine(value);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }
#endif

        public override string ToString()
            => writer is ArrayBufferWriter<char> buffer ? buffer.BuildString() : writer.ToString() ?? string.Empty;
    }
}