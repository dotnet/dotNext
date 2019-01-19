using System;
using System.Security.Cryptography;
using Xunit;

namespace DotNext
{
	public sealed class StringsTests: Assert
	{
		[Fact]
		public void IfNullOrEmptyTest()
		{
			Equal("a", "".IfNullOrEmpty("a"));
			Equal("a", default(string).IfNullOrEmpty("a"));
			Equal("b", "b".IfNullOrEmpty("a"));
		}

		[Fact]
		public void RandomStringTest()
		{
			const string AllowedChars = "abcd123456789";
			var rnd = new Random();
			var str = rnd.RandomString(AllowedChars, 6);
			Equal(6, str.Length);
			Console.WriteLine(str);
			using(var generator = new RNGCryptoServiceProvider())
			{
				str = generator.RandomString(AllowedChars, 7);
			}
			Console.WriteLine(str);
		}
	}
}
