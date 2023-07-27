using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using HttpHeaders = System.Net.Http.Headers.HttpHeaders;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;

internal abstract class HttpMessage
{
    // Perf: length = 64 which is a power of 2 (see RandomExtensions.NextString impl)
    private const string RequestIdAllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@";
    private const int RequestIdLength = 32;

    // request - represents ID of sender node
    private const string NodeIdHeader = "X-Raft-Node-ID";

    // request - represents request message type
    private const string MessageTypeHeader = "X-Raft-Message-Type";

    // request - represents unique request identifier
    private const string RequestIdHeader = "X-Request-ID";

    private protected static readonly ValueParser<long> Int64Parser = long.TryParse;
    private protected static readonly ValueParser<int> Int32Parser = int.TryParse;
    private static readonly ValueParser<ClusterMemberId> ClusterMemberIdParser = ClusterMemberId.TryParse;
    private protected static readonly ValueParser<bool> BooleanParser = bool.TryParse;
    private static readonly ValueParser<string> StringParser = ParseString;

    private protected delegate bool ValueParser<T>(string str, [MaybeNullWhen(false)] out T value)
        where T : notnull;

    internal readonly string Id;
    internal readonly ClusterMemberId Sender;

    private protected HttpMessage(in ClusterMemberId sender)
    {
        Sender = sender;
        Id = Random.Shared.NextString(RequestIdAllowedChars, RequestIdLength);
    }

    private protected HttpMessage(IDictionary<string, StringValues> headers)
    {
        Sender = ParseHeader(headers, NodeIdHeader, ClusterMemberIdParser);
        Id = ParseHeader(headers, RequestIdHeader);
    }

    internal static string GetMessageType(HttpRequest request)
        => ParseHeader(request.Headers, MessageTypeHeader);

    [RequiresPreviewFeatures]
    internal static void SetMessageType<TMessage>(HttpRequestMessage request)
        where TMessage : class, IHttpMessage
        => request.Headers.Add(MessageTypeHeader, TMessage.MessageType);

    protected void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(NodeIdHeader, Sender.ToString());
        request.Headers.Add(RequestIdHeader, Id);
        request.Method = HttpMethod.Post;
    }

    // serves as a default implementation of IHttpMessage.IsMemberUnavailable
    public static bool IsMemberUnavailable(HttpStatusCode? code)
        => code is null or HttpStatusCode.InternalServerError;

    private protected static async Task<bool> ParseBoolResponseAsync(HttpResponseMessage response, CancellationToken token)
        => bool.TryParse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), out var result)
            ? result
            : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

    private protected static async Task<T> ParseEnumResponseAsync<T>(HttpResponseMessage response, CancellationToken token)
        where T : struct, Enum
        => Enum.TryParse<T>(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), out var result)
            ? result
            : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

    private protected static Task SaveResponseAsync(HttpResponse response, bool result, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        return response.WriteAsync(result.ToString(InvariantCulture), token);
    }

    private protected static Task SaveResponseAsync<T>(HttpResponse response, T result, CancellationToken token)
        where T : struct, Enum
    {
        response.StatusCode = StatusCodes.Status200OK;
        return response.WriteAsync(Enum.GetName<T>(result) ?? string.Empty, token);
    }

    private static bool ParseString(string input, [MaybeNullWhen(false)] out string output)
    {
        output = input;
        return true;
    }

    // we have two versions of this method to avoid allocations caused by StringValues.GetEnumerator()
    private static Optional<T> ParseHeaderCore<T>(IDictionary<string, StringValues>? headers, string headerName, ValueParser<T> parser)
        where T : notnull
    {
        if (headers is not null && headers.TryGetValue(headerName, out var values))
        {
            foreach (var header in values)
            {
                if (parser(header, out var result))
                    return result;
            }
        }

        return Optional<T>.None;
    }

    private protected static T ParseHeader<T>(IDictionary<string, StringValues>? headers, string headerName, ValueParser<T> parser)
        where T : notnull
    {
        var result = ParseHeaderCore(headers, headerName, parser);
        return result.HasValue
            ? result.OrDefault()!
            : throw new RaftProtocolException(ExceptionMessages.MissingHeader(headerName));
    }

    private protected static T? ParseHeaderAsNullable<T>(IDictionary<string, StringValues>? headers, string headerName, ValueParser<T> parser)
        where T : struct
        => ParseHeaderCore(headers, headerName, parser).OrNull();

    private protected static string ParseHeader(IDictionary<string, StringValues>? headers, string headerName)
        => ParseHeader(headers, headerName, StringParser);

    private static Optional<T> ParseHeaderCore<T>(HttpHeaders? headers, string headerName, ValueParser<T> parser)
        where T : notnull
    {
        if (headers is not null && headers.TryGetValues(headerName, out var values))
        {
            foreach (var header in values)
            {
                if (parser(header, out var result))
                    return result;
            }
        }

        return Optional<T>.None;
    }

    private protected static T ParseHeader<T>(HttpHeaders? headers, string headerName, ValueParser<T> parser)
        where T : notnull
    {
        var result = ParseHeaderCore(headers, headerName, parser);
        return result.HasValue
            ? result.OrDefault()!
            : throw new RaftProtocolException(ExceptionMessages.MissingHeader(headerName));
    }

    private protected static string ParseHeader(HttpHeaders? headers, string headerName)
        => ParseHeader(headers, headerName, StringParser);
}