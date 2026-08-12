#nullable enable

using GreenBox.I18n;
using UnityEngine;

/// <summary>
/// Adds Unity-specific operations to engine-independent locale metadata.
/// </summary>
public static class I18nRuntimeLocaleExtensions
{
    /// <summary>
    /// Gets the Unity sprite assigned as the locale icon.
    /// </summary>
    /// <param name="locale">A locale from <see cref="I18n.Locales"/>.</param>
    /// <returns>The resolved icon, or <see langword="null"/> when no icon is assigned.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="locale"/> is null.
    /// </exception>
    /// <exception cref="System.InvalidCastException">
    /// Thrown when the assigned locale icon is not a <see cref="Sprite"/>.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the project catalog cannot be loaded or the icon was not compiled.
    /// </exception>
    public static Sprite? GetIcon(this I18nRuntimeLocale locale)
    {
        return I18n.ResolveLocaleIcon(locale);
    }
}
