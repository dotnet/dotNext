using System.Diagnostics.CodeAnalysis;
using System.Net;
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

    private protected delegate bool ValueParser<T>(string str, [MaybeNullWhen(false)] out T value);

    internal readonly string Id;
    internal readonly ClusterMemberId Sender;
    internal readonly string MessageType;

    private protected HttpMessage(string messageType, in ClusterMemberId sender)
    {
        Sender = sender;
        MessageType = messageType;
        Id = Random.Shared.NextString(RequestIdAllowedChars, RequestIdLength);
    }

    private protected HttpMessage(IDictionary<string, StringValues> headers)
    {
        Sender = ParseHeader(headers, NodeIdHeader, ClusterMemberIdParser);
        MessageType = GetMessageType(headers);
        Id = ParseHeader(headers, RequestIdHeader);
    }

    /// <summary>
    /// Interprets <see cref="HttpRequestException"/> produced by HTTP client.
    /// </summary>
    /// <returns><see langword="true"/> to handle the response as <see cref="MemberUnavailableException"/>.</returns>
    internal virtual bool IsMemberUnavailable(HttpStatusCode? code)
        => code is null or HttpStatusCode.InternalServerError;

    private static string GetMessageType(IDictionary<string, StringValues> headers)
        => ParseHeader(headers, MessageTypeHeader);

    internal static string GetMessageType(HttpRequest request)
        => GetMessageType(request.Headers);

    internal virtual void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(NodeIdHeader, Sender.ToString());
        request.Headers.Add(MessageTypeHeader, MessageType);
        request.Headers.Add(RequestIdHeader, Id);
        request.Method = HttpMethod.Post;
    }

    private protected static async Task<bool> ParseBoolResponse(HttpResponseMessage response, CancellationToken token)
        => bool.TryParse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), out var result)
            ? result
            : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

    private protected static async Task<T> ParseEnumResponse<T>(HttpResponseMessage response, CancellationToken token)
        where T : struct, Enum
        => Enum.TryParse<T>(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), out var result)
            ? result
            : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

    private protected static Task SaveResponse(HttpResponse response, bool result, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        return response.WriteAsync(result.ToString(InvariantCulture), token);
    }

    private protected static Task SaveResponse<T>(HttpResponse response, T result, CancellationToken token)
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

    private protected static T? ParseHeaderAsNullable<T>(HttpHeaders? headers, string headerName, ValueParser<T> parser)
        where T : struct
        => ParseHeaderCore(headers, headerName, parser).OrNull();

    private protected static string ParseHeader(HttpHeaders? headers, string headerName)
        => ParseHeader(headers, headerName, StringParser);
}