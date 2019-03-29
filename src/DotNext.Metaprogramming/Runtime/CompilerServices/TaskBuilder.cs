using System;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    using Reflection;

    internal struct TaskBuilder
    {
        private Type resultType;

        internal TaskBuilder(Type taskType)
        {
            if(taskType == typeof(ValueTask))
            {
                IsValueTask = true;
            }
            else if
        }

        internal Type ResultType
        {
            get => resultType ?? typeof(void);
            set => resultType = value;
        }

        internal bool IsValueTask { get; set; }
    }
}
