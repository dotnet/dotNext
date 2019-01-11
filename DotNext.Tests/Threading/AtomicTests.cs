using Xunit;

namespace DotNext.Threading
{
	public sealed class AtomicTests : Assert
	{
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
	}
}
