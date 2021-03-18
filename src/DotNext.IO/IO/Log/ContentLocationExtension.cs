using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents physical location of the log entry content.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ContentLocationExtension : ISupplier<Uri>
    {
        private static readonly Uri Empty = new Uri("/", UriKind.Relative);
        private readonly Uri? location;

        /// <summary>
        /// Initializes a new extension representing location of the log entry content.
        /// </summary>
        /// <param name="location">The absolute location of the log entry.</param>
        public ContentLocationExtension(Uri location)
            => this.location = location ?? throw new ArgumentNullException(nameof(location));

        /// <summary>
        /// Initializes a new extension representing location of the log entry content.
        /// </summary>
        /// <param name="file">The absolute location of the log entry in the file system.</param>
        public ContentLocationExtension(FileInfo file)
        {
            var fileName = string.Concat(Uri.UriSchemeFile, Uri.SchemeDelimiter, file.FullName);
            this.location = new Uri(fileName);
        }

        /// <summary>
        /// Gets location of log entry content.
        /// </summary>
        public Uri Value => location ?? Empty;

        /// <inheritdoc />
        Uri ISupplier<Uri>.Invoke() => Value;

        /// <summary>
        /// Gets location of log entry content.
        /// </summary>
        /// <param name="location">The location of log entry content.</param>
        public static implicit operator Uri(ContentLocationExtension location) => location.Value;

        /// <summary>
        /// Gets string representation of this extension.
        /// </summary>
        /// <returns>The string representation of this extension.</returns>
        public override string ToString() => Value.ToString();
    }
}