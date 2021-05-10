using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 10)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class AtomicContainerBenchmark
    {
        private struct LargeStruct
        {
            internal Guid Field1, Field2, Field3;
        }

        private sealed class SynchronizedContainer
        {
            private LargeStruct value;

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal void Read(out LargeStruct result) => result = value;

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal void Write(in LargeStruct value) => this.value = value;
        }

        private sealed class SpinLockContainer
        {
            private LargeStruct value;
            private SpinLock spinLock;

            internal SpinLockContainer() => spinLock = new SpinLock(false);

            internal void Read(out LargeStruct result)
            {
                var lockTaken = false;
                spinLock.Enter(ref lockTaken);
                result = value;
                if (lockTaken)
                    spinLock.Exit(false);
            }

            internal void Write(in LargeStruct value)
            {
                var lockTaken = false;
                spinLock.Enter(ref lockTaken);
                this.value = value;
                if (lockTaken)
                    spinLock.Exit(false);
            }
        }

        private static Atomic<LargeStruct> VContainer = new();
        private static readonly SynchronizedContainer SContainer = new();
        private static readonly SpinLockContainer SLContainer = new();

        private static readonly LargeStruct Value = new() { Field2 = Guid.NewGuid(), Field1 = Guid.NewGuid(), Field3 = Guid.NewGuid() };

        private Thread vRead1, vRead2, vWrite;
        private Thread sRead1, sRead2, sWrite;
        private Thread lRead1, lRead2, lWrite;

        private static void VolatileRead()
        {
            LargeStruct value;
            for (var i = 0; i < 10000; i++)
                VContainer.Read(out value);
        }

        private static void SynchronizedRead()
        {
            LargeStruct value;
            for (var i = 0; i < 10000; i++)
                SContainer.Read(out value);
        }

        private static void SpinLockRead()
        {
            LargeStruct value;
            for (var i = 0; i < 10000; i++)
                SLContainer.Read(out value);
        }

        private static void VolatileWrite()
        {
            for (var i = 0; i < 1000; i++)
                VContainer.Write(in Value);
        }

        private static void SynchronizedWrite()
        {
            for (var i = 0; i < 1000; i++)
                SContainer.Write(in Value);
        }

        private static void SpinLockWrite()
        {
            for (var i = 0; i < 1000; i++)
                SLContainer.Write(in Value);
        }

        [IterationSetup]
        public void InitThreads()
        {
            sRead1 = new Thread(SynchronizedRead);
            sRead2 = new Thread(SynchronizedRead);
            sWrite = new Thread(SynchronizedWrite);

            vRead1 = new Thread(VolatileRead);
            vRead2 = new Thread(VolatileRead);
            vWrite = new Thread(VolatileWrite);

            lRead1 = new Thread(SpinLockRead);
            lRead2 = new Thread(SpinLockRead);
            lWrite = new Thread(SpinLockWrite);
        }

        [Benchmark]
        public void ReadWriteUsingSynchronizedAccess()
        {
            sWrite.Start();
            sRead1.Start();
            sRead2.Start();

            sWrite.Join();
            sRead1.Join();
            sRead2.Join();
        }

        [Benchmark]
        public void ReadWriteUsingAtomicAccess()
        {
            vWrite.Start();
            vRead1.Start();
            vRead2.Start();

            vWrite.Join();
            vRead1.Join();
            vRead2.Join();
        }

        [Benchmark]
        public void ReadWriteUsingSpinLock()
        {
            lWrite.Start();
            lRead1.Start();
            lRead2.Start();

            lWrite.Join();
            lRead1.Join();
            lRead2.Join();
        }
    }
}