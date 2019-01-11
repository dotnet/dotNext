using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace DotNext
{
	public sealed class CollectionsTest : Assert
	{
		public sealed class Counter<T>
		{
			public int value;

			public void Accept(T item) => value += 1;
		}

		[Fact]
		public void ForEachTest()
		{
			IList<int> list = new List<int> { 1, 10, 20 };
			var counter = new Counter<int>();
			list.ForEach(counter.Accept);
			Equal(3, counter.value);
			counter.value = 0;
			var array2 = new int[] { 1, 2, 10, 11, 15 };
			array2.ForEach(counter.Accept);
			Equal(5, counter.value);
		}

		[Fact]
		public void FirstOrNullTest()
		{
			var array = new long[0];
			var element = array.FirstOrNull();
			Null(element);
			array = new long[] { 10, 20 };
			element = array.FirstOrNull();
			Equal(10, element);
		}

		[Fact]
		public void ElementAtIndex()
		{
			var list = new LinkedList<long>();
			list.AddLast(10);
			list.AddLast(40);
			list.AddLast(100);
			list.ElementAt(2, out var element);
			Equal(100, element);
			list.ElementAt(0, out element);
			Equal(10, element);
		}

		[Fact]
		public void SkipNullsTest()
		{
			var list = new LinkedList<string>();
			list.AddLast("a");
			list.AddLast(default(string));
			list.AddLast("b");
			list.AddLast(default(string));
			Equal(4, list.Count);
			var array = list.SkipNulls().ToArray();
			Equal(2, array.Length);
			True(Array.Exists(array, "a".Equals));
			True(Array.Exists(array, "b".Equals));
		}

		[Fact]
		public void ToStringTest()
		{
			var array = new int[] { 10, 20, 30 };
			var str = array.ToString(":");
			Equal("10:20:30", str);
		}

		[Fact]
		public void ToArrayTest()
		{
			var list = new List<long>() { 10, 40, 100 };
			var array = list.ToArray(i => i.ToString());
			True(array.SequenceEqual(new string[] { "10", "40", "100" }));
		}
	}
}
