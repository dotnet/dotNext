using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class ConcurrentObjectPoolTests : Assert
    {
        [Fact]
        public static void RoundRobinAlgorithmCheck()
        {
            using (var pool = new ConcurrentObjectPool<string>(new[] { "One", "Two", "Three" }))
            {
                var rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("Two", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("Three", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
            }
        }

        private sealed class StringFactory
        {
            private int sequenceNumber;

            internal string CreateString()
            {
                switch (sequenceNumber++)
                {
                    case 0: return "One";
                    case 1: return "Two";
                    case 2: return "Three";
                    default: throw new Exception();
                }
            }
        }

        [Fact]
        public static void ShortestJobFirstAlgorithmCheck()
        {
            using (var pool = new ConcurrentObjectPool<string>(3, new StringFactory().CreateString))
            {
                //check for reuse when rent/dispose happening
                var rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
                rental = pool.Rent();
                Equal("One", rental.Resource);
                rental.Dispose();
                //check for lazy initialization if several objects are in rent
                rental = pool.Rent();
                var rental2 = pool.Rent();
                var rental3 = pool.Rent();
                Equal("One", rental.Resource);
                Equal("Two", rental2.Resource);
                Equal("Three", rental3.Resource);
            }
        }

        [Fact]
        public static void StarvationLoadTest()
        {
            using (var pool = new ConcurrentObjectPool<string>(3, new StringFactory().CreateString))
            {
                for (var i = 0; i < 500; i++)
                {
                    ConcurrentObjectPool<string>.IRental rental1 = pool.Rent(), rental2 = pool.Rent(), rental3 = pool.Rent();
                    Equal("One", rental1.Resource);
                    Equal("Two", rental2.Resource);
                    Equal("Three", rental3.Resource);
                    rental3.Dispose();
                    rental2.Dispose();
                    rental1.Dispose();
                }
            }
        }

        [Fact]
        public static void StarvationCheck()
        {
            using (var pool = new ConcurrentObjectPool<string>(3, new StringFactory().CreateString))
            {
                ConcurrentObjectPool<string>.IRental rental1 = pool.Rent(), rental2 = pool.Rent(), rental3 = pool.Rent();
                Equal("One", rental1.Resource);
                Equal("Two", rental2.Resource);
                Equal("Three", rental3.Resource);
                rental3.Dispose();
                rental2.Dispose();
                rental1.Dispose();
                //Use first two objects in the pool, third will be destroyed automatically
                rental2 = pool.Rent();
                rental2.Dispose();
                rental1 = pool.Rent();
                rental1.Dispose();
                rental1 = pool.Rent();
                rental2 = pool.Rent();
                Throws<Exception>(pool.Rent);
            }
        }

        [Fact]
        public static void LackOfFreeObjectsRR()
        {
            using (var pool = new ConcurrentObjectPool<string>(new[] { "One", "Two" }))
            {
                var rental1 = pool.Rent();
                Equal("One", rental1.Resource);
                var rental2 = pool.Rent();
                Equal("Two", rental2.Resource);
                var t = new Thread(() =>
                {
                    var rental3 = pool.Rent();
                    Equal("Two", rental3.Resource);
                    rental3.Dispose();
                })
                {
                    IsBackground = true
                };
                t.Start();
                Thread.Sleep(500);
                //now t trying to rent a new object
                Equal(1, pool.WaitCount);
                rental2.Dispose();  //pass object to thread t
                t.Join();
                rental2 = pool.Rent();
                Equal("Two", rental2.Resource);
                rental1.Dispose();
                rental2.Dispose();
            }
        }
    }
}
