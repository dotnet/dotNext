using System;
using Xunit;

namespace DotNext.Collections.Generic
{
    public sealed class UnmanagedListTest: Assert
    {
        [Fact]
        public static void AddRead()
        {
            using (var list = new UnmanagedList<int>(10))
            {
                Empty(list);
                list.Add(10);
                list.Add(20);
                Equal(2, list.Count);
                Equal(0, list.IndexOf(10));
                Equal(1, list.IndexOf(20));
                Equal(-1, list.IndexOf(0));
                Contains(10, list);
                Contains(20, list);
                DoesNotContain(0, list);
            }
        }

        [Fact]
        public static void Indexer()
        {
            var list = new UnmanagedList<int>(10);
            try
            {
                list.Add(20);
                list.Add(40);
                Equal(20, list[0]);
                Equal(40, list[1]);
                list[0] = 1754;
                Equal(1754, list[0]);
                Equal(40, list[1]);
            }
            finally
            {
                list.Dispose();
            }
        }

        [Fact]
        public static void InsertRemove()
        {
            using (var list = new UnmanagedList<int>(10))
            {
                list.Add(40);
                list.Add(50);
                //insertion
                list.Insert(1, 90);
                Equal(3, list.Count);
                Equal(40, list[0]);
                Equal(90, list[1]);
                Equal(50, list[2]);
                list.Insert(0, 56);

                Equal(4, list.Count);
                Equal(56, list[0]);
                Equal(40, list[1]);
                Equal(90, list[2]);
                Equal(50, list[3]);
                //removal
                list.RemoveAt(0);
                Equal(3, list.Count);
                Equal(40, list[0]);
                Equal(90, list[1]);
                Equal(50, list[2]);

                True(list.Remove(90));
                Equal(2, list.Count);
                Equal(40, list[0]);
                Equal(50, list[1]);
            }
        }

        [Fact]
        public static void Capacity()
        {
            using (var list = new UnmanagedList<int>(3))
            {
                Equal(3, list.Capacity);
                Empty(list);
                list.Add(10);
                list.Add(20);
                list.Add(30);
                list.Add(40);
                True(list.Capacity > 3);
                Equal(4, list.Count);
            }
        }

        [Fact]
        public static void Enumeration()
        {
            using (var list = new UnmanagedList<int>(3))
            {
                list.Add(10);
                list.Add(20);
                list.Add(30);
                list.Add(40);
                var index = 0;
                foreach(var item in list)
                    switch(index++)
                    {
                        case 0:
                            Equal(10, item);
                            continue;
                        case 1:
                            Equal(20, item);
                            continue;
                        case 2:
                            Equal(30, item);
                            continue;
                        case 3:
                            Equal(40, item);
                            continue;
                        default:
                            throw new Exception();
                    }
            }
        }
    }
}
