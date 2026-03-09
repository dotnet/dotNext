namespace DotNext;

internal static class ResultExtensions
{
    extension(Result)
    {
        public static Result<bool>.Ok True => true;

        public static Result<bool>.Ok False => false;
    }
}