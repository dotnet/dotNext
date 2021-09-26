using System.Globalization;
using System.Resources;
using System.Runtime.InteropServices;

namespace DotNext.Resources;

/// <summary>
/// Represents resource entry.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ResourceEntry
{
    internal ResourceEntry(ResourceManager manager, string name)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets name of the resource entry.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets resource manager providing access to the entry.
    /// </summary>
    public ResourceManager Manager { get; }

    /// <summary>
    /// Returns resource string.
    /// </summary>
    /// <param name="culture">An object that represents the culture for which the resource is localized.</param>
    /// <returns>The resource string.</returns>
    /// <exception cref="InvalidOperationException">The value of the specified resource is not a string.</exception>
    /// <exception cref="MissingManifestResourceException">No usable set of resources has been found, and there are no resources for the default culture.</exception>
    /// <exception cref="MissingSatelliteAssemblyException">The default culture's resources reside in a satellite assembly that could not be found.</exception>
    public string AsString(CultureInfo? culture = null)
        => Manager.GetString(Name, culture ?? CultureInfo.CurrentUICulture) ?? throw new InvalidOperationException(ExceptionMessages.ResourceEntryIsNull(Name));

    /// <summary>
    /// Returns formatted resource string.
    /// </summary>
    /// <param name="args">The formatting arguments.</param>
    /// <returns>The formatter resource string.</returns>
    public string Format(params object?[] args)
    {
        var culture = CultureInfo.CurrentUICulture;
        return string.Format(culture, AsString(culture), args);
    }

    /// <summary>
    /// Returns resource entry as a stream.
    /// </summary>
    /// <param name="culture">An object that represents the culture for which the resource is localized.</param>
    /// <returns>The stream representing resource entry.</returns>
    /// <exception cref="InvalidOperationException">The value of the specified resource is not <see cref="Stream"/>.</exception>
    /// <exception cref="MissingManifestResourceException">No usable set of resources has been found, and there are no resources for the default culture.</exception>
    /// <exception cref="MissingSatelliteAssemblyException">The default culture's resources reside in a satellite assembly that could not be found.</exception>
    public Stream AsStream(CultureInfo? culture = null)
        => Manager.GetStream(Name, culture ?? CultureInfo.CurrentUICulture) ?? throw new InvalidOperationException(ExceptionMessages.ResourceEntryIsNull(Name));

    /// <summary>
    /// Deserializes resource entry.
    /// </summary>
    /// <typeparam name="T">The type of the resource entry.</typeparam>
    /// <param name="culture">An object that represents the culture for which the resource is localized.</param>
    /// <returns>The deserialized resource entry.</returns>
    /// <exception cref="InvalidOperationException">The resource entry is not of type <typeparamref name="T"/>.</exception>
    /// <exception cref="MissingManifestResourceException">No usable set of resources has been found, and there are no resources for the default culture.</exception>
    /// <exception cref="MissingSatelliteAssemblyException">The default culture's resources reside in a satellite assembly that could not be found.</exception>
    public T As<T>(CultureInfo? culture = null)
        => Manager.GetObject(Name, culture) is T result ? result : throw new InvalidOperationException();

    /// <summary>
    /// Obtains resource string.
    /// </summary>
    /// <param name="entry">The resource entry.</param>
    /// <return>The resource string.</return>
    /// <exception cref="InvalidOperationException">The value of the specified resource is not a string.</exception>
    /// <exception cref="MissingManifestResourceException">No usable set of resources has been found, and there are no resources for the default culture.</exception>
    /// <exception cref="MissingSatelliteAssemblyException">The default culture's resources reside in a satellite assembly that could not be found.</exception>
    public static explicit operator string(in ResourceEntry entry)
        => entry.AsString();

    /// <summary>
    /// Obtains resource entry as a stream.
    /// </summary>
    /// <param name="entry">The resource entry.</param>
    /// <returns>The stream representing resource entry.</returns>
    /// <exception cref="InvalidOperationException">The value of the specified resource is not a string.</exception>
    /// <exception cref="MissingManifestResourceException">No usable set of resources has been found, and there are no resources for the default culture.</exception>
    /// <exception cref="MissingSatelliteAssemblyException">The default culture's resources reside in a satellite assembly that could not be found.</exception>
    public static explicit operator Stream(in ResourceEntry entry)
        => entry.AsStream();
}