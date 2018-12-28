namespace Cheats
{
    public static class Strings
    {
        public static string IfNullOrEmpty(this string str, string alt)
            => string.IsNullOrEmpty(str) ? alt : str;
    }
}