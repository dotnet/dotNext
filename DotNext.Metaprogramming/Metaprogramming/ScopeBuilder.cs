using System;
using System.Collections.Generic;
using System.Text;

namespace DotNext.Metaprogramming
{
    public sealed class ScopeBuilder
    {
        public object Local<T>(string name, bool byRef = false)
            => Local(name, byRef ? typeof(T).MakeByRefType() : typeof(T));

        public object Local(string name, Type variableType)
        {
            return null;
        }
    }
}
