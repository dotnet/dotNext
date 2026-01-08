using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace DotNext;

/// <summary>
/// Provides extensions for <see cref="AppContext"/> type.
/// </summary>
public static class AppContextExtensions
{
    /// <summary>
    /// Extends <see cref="AppContext"/> type with static members.
    /// </summary>
    extension(AppContext)
    {
        /// <summary>
        /// Checks whether the specified feature is enabled or disabled.
        /// </summary>
        /// <remarks>
        /// This method is intended to use in combination with <see cref="FeatureSwitchDefinitionAttribute"/>
        /// attribute.
        /// </remarks>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns><see langword="true"/> if feature is enabled; otherwise, <see langword="false"/>.</returns>
        /// <seealso cref="FeatureSwitchDefinitionAttribute"/>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static bool IsFeatureSupported([ConstantExpected] string featureName)
            => !AppContext.TryGetSwitch(featureName, out var isEnabled) || isEnabled;
    }
}