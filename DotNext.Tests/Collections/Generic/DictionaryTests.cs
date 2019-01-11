using Xunit;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
	public sealed class DictionaryTests: Assert
	{
		[Fact]
		public void ReplaceAllTest()
		{
			var dict = new Dictionary<string, int>()
			{
				{"a", 1 },
				{"b", 2 }
			};
			var view = dict.Convert(i => i + 10);
			Equal(11, view["a"]);
			Equal(12, view["b"]);
		}
	}
}
