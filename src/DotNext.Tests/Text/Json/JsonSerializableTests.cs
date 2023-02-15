using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace DotNext.Text.Json;

using IO;
using Runtime.Serialization;

[ExcludeFromCodeCoverage]
public sealed class JsonSerializableTests : Test
{
    private class TestFileReader : FileReader
    {
        public TestFileReader(string path)
            : base(File.OpenHandle(path, options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                handle.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TestFileReaderWithoutLengthInfo : TestFileReader, IAsyncBinaryReader
    {
        public TestFileReaderWithoutLengthInfo(string path)
            : base(path)
        {
        }

        bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
        {
            count = default;
            return false;
        }
    }

    private static async Task<Stream> SerializeToStreamAsync<TObject>(TObject obj)
        where TObject : struct, ISerializable<TObject>
    {
        var ms = new MemoryStream(2048);
        await obj.WriteToAsync(ms);
        ms.Position = 0L;
        return ms;
    }

    private static async Task<ReadOnlyMemory<byte>> SerializeToMemoryAsync<TObject>(TObject obj)
        where TObject : struct, ISerializable<TObject>
    {
        var writer = new ArrayBufferWriter<byte>(2048);
        await obj.WriteToAsync(writer);
        return writer.WrittenMemory;
    }

    private static async Task<FileReader> SerializeToFileAsync<TObject>(TObject obj, bool hideLengthInfo)
        where TObject : struct, ISerializable<TObject>
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 2048, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await obj.WriteToAsync(output);
            await output.FlushAsync();
        }

        return hideLengthInfo ? new TestFileReaderWithoutLengthInfo(path) : new TestFileReader(path);
    }

    private static void Equal(JsonSerializable<TestJsonObject> expected, JsonSerializable<TestJsonObject> actual)
    {
        Equal(expected.Value.BoolField, actual.Value.BoolField);
        Equal(expected.Value.IntegerValue, actual.Value.IntegerValue);
        Equal(expected.Value.StringField, actual.Value.StringField);
    }

    [Fact]
    public static async Task SerializeDeserializeWithStream()
    {
        var expected = new JsonSerializable<TestJsonObject>
        {
            Value = new() { BoolField = true, IntegerValue = 42, StringField = "Hello, world" }
        };

        JsonSerializable<TestJsonObject> actual;
        await using (var stream = await SerializeToStreamAsync(expected))
        {
            actual = await Serializable.ReadFromAsync<JsonSerializable<TestJsonObject>>(stream);
        }

        Equal(expected, actual);
    }

    [Fact]
    public static async Task SerializeDeserializeInMemory()
    {
        var expected = new JsonSerializable<TestJsonObject>
        {
            Value = new() { BoolField = true, IntegerValue = 42, StringField = "Hello, world" }
        };

        var memory = await SerializeToMemoryAsync(expected);
        Equal(expected, await JsonSerializable<TestJsonObject>.ReadFromAsync(IAsyncBinaryReader.Create(memory)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task SerializeDeserializeWithFile(bool hideLengthInfo)
    {
        var expected = new JsonSerializable<TestJsonObject>
        {
            Value = new() { BoolField = true, IntegerValue = 42, StringField = "Hello, world" }
        };

        JsonSerializable<TestJsonObject> actual;
        using (var reader = await SerializeToFileAsync(expected, hideLengthInfo))
        {
            actual = await JsonSerializable<TestJsonObject>.ReadFromAsync(reader);
        }

        Equal(expected, actual);
    }

    [Fact]
    public static async Task SerializeDeserializeWithPipe()
    {
        var expected = new JsonSerializable<TestJsonObject>
        {
            Value = new() { BoolField = true, IntegerValue = 42, StringField = "Hello, world" }
        };

        JsonSerializable<TestJsonObject> actual;
        await using (var stream = await SerializeToStreamAsync(expected))
        {
            actual = await Serializable.ReadFromAsync<JsonSerializable<TestJsonObject>>(PipeReader.Create(stream));
        }

        Equal(expected, actual);
    }
}