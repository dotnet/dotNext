using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.IO;

[ExcludeFromCodeCoverage]
public sealed class TextBufferReaderTests : Test
{
    private static char[] CharData { get; }

    private static char[] SmallData { get; }

    private static char[] LargeData { get; }

    static TextBufferReaderTests()
    {
        CharData = ("Char data \r\u3190 with" + Environment.NewLine + "multiple" + Environment.NewLine + "lines").ToCharArray();

        SmallData = "HELLO".ToCharArray();

        var data = new List<char>(5000);
        for (int count = 0; count < 1000; ++count)
        {
            data.AddRange(SmallData);
        }

        LargeData = data.ToArray();
    }

    public static IEnumerable<object[]> SmallDataSet()
    {
        yield return new object[] { new ReadOnlySequence<char>(SmallData) };
        yield return new object[] { ToReadOnlySequence<char>(SmallData, 3) };
    }

    public static IEnumerable<object[]> LargeDataSet()
    {
        yield return new object[] { new ReadOnlySequence<char>(LargeData) };
        yield return new object[] { ToReadOnlySequence<char>(LargeData, 500) };
    }

    public static IEnumerable<object[]> CharDataSet()
    {
        yield return new object[] { new ReadOnlySequence<char>(CharData) };
        yield return new object[] { ToReadOnlySequence<char>(CharData, 10) };
    }

    [Theory]
    [MemberData(nameof(SmallDataSet))]
    public static void EndOfStream(ReadOnlySequence<char> smallData)
    {
        using var tr = smallData.AsTextReader();
        var result = tr.ReadToEnd();
        Equal("HELLO", result);
        True(tr.Peek() == -1, "End of TextReader was not true after ReadToEnd");
    }

    [Theory]
    [MemberData(nameof(SmallDataSet))]
    public void NotEndOfStream(ReadOnlySequence<char> smallData)
    {
        using var tr = smallData.AsTextReader();
        char[] charBuff = new char[3];
        var result = tr.Read(charBuff, 0, 3);
        Equal(3, result);
        Equal("HEL", new string(charBuff));
        False(tr.Peek() == -1, "End of TextReader was true after ReadToEnd");
    }

    [Theory]
    [MemberData(nameof(LargeDataSet))]
    public static async Task ReadToEndAsync(ReadOnlySequence<char> largeData)
    {
        using var tr = largeData.AsTextReader();
        var result = await tr.ReadToEndAsync();
        Equal(5000, result.Length);
    }

    [Theory]
    [MemberData(nameof(CharDataSet))]
    public static void TestRead(ReadOnlySequence<char> charData)
    {
        using var tr = charData.AsTextReader();
        var expectedData = charData.ToArray();
        for (var count = 0; count < expectedData.Length; ++count)
        {
            var tmp = tr.Read();
            Equal(expectedData[count], tmp);
        }
    }

    [Fact]
    public static void ReadZeroCharacters()
    {
        using var tr = new ReadOnlySequence<char>(CharData).AsTextReader();
        Equal(0, tr.Read(new char[0], 0, 0));
    }

    [Fact]
    public static void EmptyInput()
    {
        using var tr = ReadOnlySequence<char>.Empty.AsTextReader();
        char[] buffer = new char[10];
        int read = tr.Read(buffer, 0, 1);
        Equal(0, read);
    }

    [Fact]
    public static void ReadFromFragmentedBuffer()
    {
        var data = ToReadOnlySequence<char>(SmallData, 3);
        using var tr = data.AsTextReader();
        var array = new char[data.Length];
        Equal(3, tr.Read(array, 0, array.Length));
        Equal(new[] { 'H', 'E', 'L' }, array[0..3]);

        Array.Clear(array);
        Equal(2, tr.Read(array, 0, array.Length));
        Equal(new[] { 'L', 'O' }, array[0..2]);
    }

    [Fact]
    public static void ReadBlockFromFragmentedBuffer()
    {
        var data = ToReadOnlySequence<char>(SmallData, 3);
        using var tr = data.AsTextReader();
        var array = new char[data.Length];
        Equal(data.Length, tr.ReadBlock(array, 0, array.Length));
        Equal(data.ToArray(), array);
    }

    [Fact]
    public static async Task ReadFromFragmentedBufferAsync()
    {
        var data = ToReadOnlySequence<char>(SmallData, 3);
        using var tr = data.AsTextReader();
        var array = new char[data.Length];
        Equal(3, await tr.ReadAsync(array, 0, array.Length));
        Equal(new[] { 'H', 'E', 'L' }, array[0..3]);

        Array.Clear(array);
        Equal(2, await tr.ReadAsync(array, 0, array.Length));
        Equal(new[] { 'L', 'O' }, array[0..2]);
    }

    [Fact]
    public static async Task ReadBlockFromFragmentedBufferAsync()
    {
        var data = ToReadOnlySequence<char>(SmallData, 3);
        using var tr = data.AsTextReader();
        var array = new char[data.Length];
        Equal(data.Length, await tr.ReadBlockAsync(array, 0, array.Length));
        Equal(data.ToArray(), array);
    }

    [Theory]
    [MemberData(nameof(CharDataSet))]
    public static void ReadLines(ReadOnlySequence<char> charData)
    {
        using var tr = charData.AsTextReader();
        string valueString = new(charData.ToArray());
        var data = tr.ReadLine();
        Equal("Char data \r\u3190 with", data);

        data = tr.ReadLine();
        Equal("multiple", data);

        data = tr.ReadLine();
        Equal("lines", data);

        Null(tr.ReadLine());
    }

    [Theory]
    [MemberData(nameof(CharDataSet))]
    public static async Task ReadLinesAsync(ReadOnlySequence<char> charData)
    {
        using var tr = charData.AsTextReader();
        string valueString = new(charData.ToArray());
        var data = await tr.ReadLineAsync();
        Equal("Char data \r\u3190 with", data);

        data = await tr.ReadLineAsync();
        Equal("multiple", data);

        data = await tr.ReadLineAsync();
        Equal("lines", data);

        Null(await tr.ReadLineAsync());
    }
}