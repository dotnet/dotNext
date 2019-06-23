using System;
using System.Buffers;
using Xunit;

namespace DotNext.Buffers
{
    public sealed class SplitterTests : Assert
    {
        [Fact]
        public static void EmptySequence()
        {
            var sequence = default(ChunkSequence<char>);
            Empty(sequence);
        }

        [Fact]
        public static void SequenceEnumeration()
        {
            var index = 0;
            foreach (var segment in new ChunkSequence<char>("abcde".AsMemory(), 2))
                switch (index++)
                {
                    case 0:
                        var array = segment.Span;
                        Equal(2, array.Length);
                        Equal('a', array[0]);
                        Equal('b', array[1]);
                        break;
                    case 1:
                        array = segment.Span;
                        Equal(2, array.Length);
                        Equal('c', array[0]);
                        Equal('d', array[1]);
                        break;
                    case 2:
                        array = segment.Span;
                        Equal(1, array.Length);
                        Equal('e', array[0]);
                        break;
                }
            Equal(3, index);
        }

        [Fact]
        public static void SplitMemory()
        {
            var sequence = (ReadOnlySequence<char>)new ChunkSequence<char>("abcde".AsMemory(), 2);
            False(sequence.IsSingleSegment);
            var index = 0;
            foreach (var segment in sequence)
                switch(index++)
                {
                    case 0:
                        var array = segment.Span;
                        Equal(2, array.Length);
                        Equal('a', array[0]);
                        Equal('b', array[1]);
                        break;
                    case 1:
                        array = segment.Span;
                        Equal(2, array.Length);
                        Equal('c', array[0]);
                        Equal('d', array[1]);
                        break;
                    case 2:
                        array = segment.Span;
                        Equal(1, array.Length);
                        Equal('e', array[0]);
                        break;
                }
            Equal(3, index);
        }
    }
}
