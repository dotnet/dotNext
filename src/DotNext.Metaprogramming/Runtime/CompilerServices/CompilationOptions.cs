using System.IO;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents lambda expression compilation options.
    /// </summary>
    public struct CompilationOptions
    {
        private FileInfo sourceCode;

        /// <summary>
        /// Determines whether the lambda expression will be compiled with the tail call optimization.
        /// </summary>
        public bool TailCall 
        {
            internal get;
            set;
        }

        /// <summary>
        /// Specify the path to the file in which the source code of the compiled expression will be written.
        /// </summary>
        public FileInfo SourceCodeOutput
        {
            set => sourceCode = value;
        }

        internal SymbolDocumentInfo CreateSymbolDocument() 
            => sourceCode is null ? null : Expression.SymbolDocument(sourceCode.FullName);
    }
}