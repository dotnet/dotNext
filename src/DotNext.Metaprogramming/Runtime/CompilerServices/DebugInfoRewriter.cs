using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class DebugInfoRewriter : ExpressionVisitor
    {
        private const string DebugInfoPrefix = ".DebugInfo";

        private readonly IReadOnlyList<string> sourceCode;
        private int lineCursor;

        internal DebugInfoRewriter(SymbolDocumentInfo sourceCode)
        {
            this.sourceCode = File.ReadAllLines(sourceCode.FileName);
            lineCursor = 0;
        }

        protected override Expression VisitDebugInfo(DebugInfoExpression node)
        {
            //adjust line number
            for(var lineNumber = lineCursor; lineNumber < sourceCode.Count; lineNumber++)
            {
                var lineOfCode = sourceCode[lineNumber];
                //the line of code contains debug info
                //rewrite DebugInfo with correct line number increased by 1
                if(lineOfCode.Contains(DebugInfoPrefix))
                {
                    lineOfCode = sourceCode[++lineNumber];
                    lineNumber += 1;    //line number is 1-based, not zero-based
                    return Expression.DebugInfo(node.Document, lineNumber, 1, lineNumber, lineOfCode.Length);
                }
            }
            return node;
        }
    }
}