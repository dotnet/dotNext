using System.CommandLine.Binding;

namespace DotNext.Maintenance.CommandLine.Binding;

using IO;

/// <summary>
/// Provides predefined bindings for the command handler parameters.
/// </summary>
public static class DefaultBindings
{
    /// <summary>
    /// Gets a descriptor that allows to obtain <see cref="IMaintenanceConsole"/>
    /// for bound parameter.
    /// </summary>
    public static IValueDescriptor<IMaintenanceConsole> Console { get; } = new ConsoleBinder();

    /// <summary>
    /// Gets a descriptor that allows to obtain <see cref="IMaintenanceSession"/>
    /// for bound parameter.
    /// </summary>
    public static IValueDescriptor<IMaintenanceSession> Session { get; } = new SessionBinder();

    private sealed class ConsoleBinder : IValueDescriptor<IMaintenanceConsole>, IValueSource
    {
        bool IValueSource.TryGetValue(IValueDescriptor valueDescriptor, BindingContext bindingContext, out object? boundValue)
            => (boundValue = bindingContext.Console as IMaintenanceConsole) is not null;

        bool IValueDescriptor.HasDefaultValue => false;

        object? IValueDescriptor.GetDefaultValue() => null;

        Type IValueDescriptor.ValueType => typeof(IMaintenanceConsole);

        string IValueDescriptor.ValueName => nameof(MaintenanceConsole);
    }

    private sealed class SessionBinder : IValueDescriptor<IMaintenanceSession>, IValueSource
    {
        bool IValueSource.TryGetValue(IValueDescriptor valueDescriptor, BindingContext bindingContext, out object? boundValue)
            => (boundValue = (bindingContext.Console as IMaintenanceConsole)?.Session) is not null;

        bool IValueDescriptor.HasDefaultValue => false;

        object? IValueDescriptor.GetDefaultValue() => null;

        Type IValueDescriptor.ValueType => typeof(IMaintenanceSession);

        string IValueDescriptor.ValueName => "MaintenanceSession";
    }
}