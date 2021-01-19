﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class StringExtensionsTests : Test
    {
        [Fact]
        public static void IfNullOrEmptyTest()
        {
            Equal("a", "".IfNullOrEmpty("a"));
            Equal("a", default(string).IfNullOrEmpty("a"));
            Equal("b", "b".IfNullOrEmpty("a"));
        }

        [Fact]
        public static void RandomStringTest()
        {
            const string AllowedChars = "abcd123456789";
            var rnd = new Random();
            var str = rnd.NextString(AllowedChars, 6);
            Equal(6, str.Length);
            using (var generator = new RNGCryptoServiceProvider())
            {
                str = generator.NextString(AllowedChars, 7);
            }
            Equal(7, str.Length);
        }

        [Fact]
        public static void ReverseTest()
        {
            Equal("cba", "abc".Reverse());
            Equal("", "".Reverse());
        }

        [Fact]
        public static void TrimLengthTest()
        {
            Equal("ab", "abcd".TrimLength(2));
        }

        [Fact]
        public static void Substring()
        {
            Equal("abcd"[1..2], "abcd".Substring(1..2));
        }
    }
}
