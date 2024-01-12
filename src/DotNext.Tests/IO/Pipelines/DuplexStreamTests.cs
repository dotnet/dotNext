using System.IO.Pipelines;

namespace DotNext.IO.Pipelines;

public sealed class DuplexStreamTests : Test
{
    [Fact]
    public static async Task CopyToDuplexStreamAsync()
    {
        var expected = RandomBytes(256);
        await using var source = new MemoryStream();
        source.Write(expected);
        source.Position = 0L;

        var pipe = new DuplexPipe();
        await using var destination = new DuplexStream(pipe, leaveOutputOpen: true);
        await source.CopyToAsync(destination);
        await pipe.Output.Writer.CompleteAsync();

        source.Position = 0L;
        await pipe.Output.Reader.CopyToAsync(source);
        Equal(expected.Length, source.Length);
        Equal(expected, source.ToArray());
    }

    private sealed class DuplexPipe : IDuplexPipe
    {
        internal readonly Pipe Input = new();
        internal readonly Pipe Output = new();

        PipeReader IDuplexPipe.Input => Input.Reader;

        PipeWriter IDuplexPipe.Output => Output.Writer;
    }
}