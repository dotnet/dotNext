using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    internal struct Octuple<T>: IEnumerator<T>
    {
        internal const short Count = 8;
        internal T First, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth;
        private short index;

        private T Current
        {
            get
            {
                switch(index)
                {
                    case 0:
                    case 1:
                        return First;
                    case 2:
                        return Second;
                    case 3:
                        return Third;
                    case 4:
                        return Fourth;
                    case 5:
                        return Fifth;
                    case 6:
                        return Sixth;
                    case 7:
                        return Seventh;
                    default:
                        return Eighth;
                }
            }
        }

        T IEnumerator<T>.Current => Current;

        object IEnumerator.Current => Current;

        bool IEnumerator.MoveNext()
        {
            index += 1;
            return index <= Count;
        }

        void IEnumerator.Reset() => index = 0;

        void IDisposable.Dispose() => this = default;
    }
}