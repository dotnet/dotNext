using Xunit;

namespace DotNext.Threading
{
	public sealed class AtomicTests : Assert
	{
        [Fact]
        public void AtomicArrayTest()
        {
            var array = new[] { "a", "b" };
            array.UpdateAndGet(1, s => s + "c");
            Equal("bc", array.VolatileGet(1));
        }

        [Fact]
        public void AtomicFloatTest()
        {
            float i = 10F;
            Equal(11F, i.IncrementAndGet());
            Equal(10F, i.DecrementAndGet());
            i = 20F;
            True(i.CompareAndSet(20F, 30F));
            Equal(30F, i);
            False(i.CompareAndSet(20F, 50F));
            Equal(30F, i);
            Equal(80F, i.Add(50F));
            Equal(80F, i);
            Equal(80F, i.GetAndAccumulate(10F, (x, y) => x + y));
            Equal(90F, i);
            Equal(10F, i.AccumulateAndGet(80F, (x, y) => x - y));
            Equal(10F, i);
            Equal(10F, i.GetAndSet(25F));
            Equal(25F, i);
            Equal(42F, i.UpdateAndGet(current => 42F));
            Equal(42F, i.GetAndUpdate(current => 52F));
            Equal(52F, i);
        }

        [Fact]
        public void AtomicDoubleTest()
        {
            double i = 10D;
            Equal(11D, i.IncrementAndGet());
            Equal(10D, i.DecrementAndGet());
            i = 20D;
            True(i.CompareAndSet(20D, 30D));
            Equal(30D, i);
            False(i.CompareAndSet(20D, 50D));
            Equal(30D, i);
            Equal(80D, i.Add(50D));
            Equal(80D, i);
            Equal(80D, i.GetAndAccumulate(10D, (x, y) => x + y));
            Equal(90D, i);
            Equal(10D, i.AccumulateAndGet(80D, (x, y) => x - y));
            Equal(10D, i);
            Equal(10D, i.GetAndSet(25D));
            Equal(25D, i);
            Equal(42D, i.UpdateAndGet(current => 42D));
            Equal(42D, i.GetAndUpdate(current => 52D));
            Equal(52D, i);
        }

        [Fact]
		public void AtomicIntTest()
		{
			var i = 10;
			Equal(11, i.IncrementAndGet());
			Equal(10, i.DecrementAndGet());
			i = 20;
			True(i.CompareAndSet(20, 30));
			Equal(30, i);
			False(i.CompareAndSet(20, 50));
			Equal(30, i);
			Equal(80, i.Add(50));
			Equal(80, i);
			Equal(80, i.GetAndAccumulate(10, (x, y) => x + y));
			Equal(90, i);
			Equal(10, i.AccumulateAndGet(80, (x, y) => x - y));
			Equal(10, i);
			Equal(10, i.GetAndSet(25));
			Equal(25, i);
			Equal(42, i.UpdateAndGet(current => 42));
			Equal(42, i.GetAndUpdate(current => 52));
			Equal(52, i);
		}

		[Fact]
		public void AtomicLongTest()
		{
			var i = 10L;
			Equal(11L, i.IncrementAndGet());
			Equal(10L, i.DecrementAndGet());
			i = 20L;
			True(i.CompareAndSet(20L, 30L));
			Equal(30L, i);
			False(i.CompareAndSet(20L, 50L));
			Equal(30L, i);
			Equal(80L, i.Add(50L));
			Equal(80L, i);
			Equal(80L, i.GetAndAccumulate(10L, (x, y) => x + y));
			Equal(90L, i);
			Equal(10L, i.AccumulateAndGet(80L, (x, y) => x - y));
			Equal(10L, i);
			Equal(10L, i.GetAndSet(25L));
			Equal(25L, i);
			Equal(42L, i.UpdateAndGet(current => 42L));
			Equal(42L, i.GetAndUpdate(current => 52L));
			Equal(52L, i);
		}

		[Fact]
		public void AtomicReferenceTest()
		{
			var stref = new AtomicReference<string>("");
			Equal("", stref.Value);
			Empty(stref.GetAndSet(null));
			Null(stref.Value);
			NotEmpty(stref.SetAndGet("Hello"));
			Equal("Hello, world!", stref.AccumulateAndGet(", world!", (x, y) => x + y));
			Equal("Hello, world!", stref.Value);
			Equal("Hello, world!", stref.GetAndUpdate(current => ""));
			Empty(stref.Value);
			stref.Value = null;
			Equal("Hello", stref.SetIfNull(() => "Hello"));
			Equal("Hello", stref.SetIfNull(() => ""));
			Equal("Hello", stref.Value);
		}

		[Fact]
		public void AtomicBooleanTest()
		{
			var value = new AtomicBoolean(false);
			True(value.FalseToTrue());
			False(value.FalseToTrue());
			True(value.TrueToFalse());
			False(value.TrueToFalse());
			True(value.NegateAndGet());
			True(value.GetAndNegate());
			False(value.Value);
		}
	}
}
