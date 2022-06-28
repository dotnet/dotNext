using System.CommandLine.Binding;

namespace DotNext.Maintenance.CommandLine.Binding;

using IO;

/// <summary>
/// Provides predefined bindings for the command handler parameters.
/// </summary>
public static class DefaultBindings
{
    /// <summary>
    /// Gets a descriptor that allows to obtain <see cref="IManagementConsole"/>
    /// for bound parameter.
    /// </summary>
    public static IValueDescriptor<IManagementConsole> Console { get; } = new ConsoleBinder();

    /// <summary>
    /// Gets a descriptor that allows to obtain <see cref="IManagementSession"/>
    /// for bound parameter.
    /// </summary>
    public static IValueDescriptor<IManagementSession> Session { get; } = new SessionBinder();

    private sealed class ConsoleBinder : IValueDescriptor<IManagementConsole>, IValueSource
    {
        bool IValueSource.TryGetValue(IValueDescriptor valueDescriptor, BindingContext bindingContext, out object? boundValue)
            => (boundValue = bindingContext.Console as IManagementConsole) is not null;

        bool IValueDescriptor.HasDefaultValue => false;

        object? IValueDescriptor.GetDefaultValue() => null;

        Type IValueDescriptor.ValueType => typeof(IManagementConsole);

        string IValueDescriptor.ValueName => nameof(ManagementConsole);
    }

    private sealed class SessionBinder : IValueDescriptor<IManagementSession>, IValueSource
    {
        bool IValueSource.TryGetValue(IValueDescriptor valueDescriptor, BindingContext bindingContext, out object? boundValue)
            => (boundValue = (bindingContext.Console as IManagementConsole)?.Session) is not null;

        bool IValueDescriptor.HasDefaultValue => false;

        object? IValueDescriptor.GetDefaultValue() => null;

        Type IValueDescriptor.ValueType => typeof(IManagementSession);

        string IValueDescriptor.ValueName => "ManagementSession";
    }
}