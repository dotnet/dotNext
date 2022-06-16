using System.Text.Json;

namespace DotNext.Text.Json;

internal static class JsonUtils
{
    internal static readonly JsonReaderOptions DefaultReaderOptions;
    internal static readonly JsonWriterOptions DefaultWriterOptions;

    static JsonUtils()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        DefaultReaderOptions = options.GetReaderOptions();
        DefaultWriterOptions = options.GetWriterOptions();
    }

    internal static JsonReaderOptions GetReaderOptions(this JsonSerializerOptions options) => new()
    {
        AllowTrailingCommas = options.AllowTrailingCommas,
        CommentHandling = options.ReadCommentHandling,
        MaxDepth = options.MaxDepth,
    };

    internal static JsonWriterOptions GetWriterOptions(this JsonSerializerOptions options) => new()
    {
        Indented = options.WriteIndented,
        Encoder = options.Encoder,
        SkipValidation = false,
    };
}