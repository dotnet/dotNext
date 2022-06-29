namespace DotNext.Maintenance.CommandLine;

internal static class Helpers
{
    internal static Func<IServiceProvider, T> GetValueProvider<T>(T value)
        where T : notnull
        => value.Convert<T>;

    private static T Convert<T>(this object value, IServiceProvider provider)
        => (T)value;
}