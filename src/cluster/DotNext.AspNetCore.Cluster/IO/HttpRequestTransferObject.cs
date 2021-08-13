using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DotNext.IO
{
    /// <summary>
    /// Represents HTTP request as DTO.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly struct HttpRequestTransferObject : IDataTransferObject, ISupplier<HttpRequest>
    {
        /// <summary>
        /// Wraps <see cref="HttpRequest"/> into <see cref="IDataTransferObject"/>.
        /// </summary>
        /// <param name="request">The request object to be wrapped.</param>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        public HttpRequestTransferObject(HttpRequest request)
            => Request = request ?? throw new ArgumentNullException(nameof(request));

        /// <summary>
        /// Gets the request associated with this object.
        /// </summary>
        public HttpRequest Request { get; }

        /// <inhertidoc/>
        long? IDataTransferObject.Length => Request.ContentLength;

        /// <inhertidoc/>
        bool IDataTransferObject.IsReusable => false;

        /// <inhertidoc />
        HttpRequest ISupplier<HttpRequest>.Invoke() => Request;

        /// <inhertidoc />
        public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            where TWriter : notnull, IAsyncBinaryWriter
            => new(writer.CopyFromAsync(Request.BodyReader, token));

        /// <inhertidoc />
        public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
            => IDataTransferObject.TransformAsync<TResult, TTransformation>(Request.BodyReader, transformation, token);

        /// <inheritdoc />
        public override string? ToString() => Request?.ToString();

        /// <inheritdoc />
        public override bool Equals(object? other) => other is HttpRequestTransferObject dto && Equals(Request, dto.Request);

        /// <inheritdoc />
        public override int GetHashCode() => Request?.GetHashCode() ?? 0;

        /// <summary>
        /// Wraps <see cref="HttpRequest"/> into <see cref="IDataTransferObject"/>.
        /// </summary>
        /// <param name="request">The request object to be wrapped.</param>
        /// <returns>The request accessible as <see cref="IDataTransferObject"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        public static implicit operator HttpRequestTransferObject(HttpRequest request) => new(request);
    }
}