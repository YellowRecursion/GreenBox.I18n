#nullable enable

using System;
using System.Collections.Generic;
using GreenBox.I18n;
using GreenBox.I18n.Unity;
using UnityEngine;

/// <summary>
/// Provides short, project-wide access to the active Unity localization runtime.
/// </summary>
public static class i18n
{
    /// <summary>
    /// Text returned for an unassigned localization key.
    /// </summary>
    public const string NonePlaceholder = "[none]";

    private static I18nRuntime? _runtime;
    private static bool _hasWarnedAboutNone;

    /// <summary>
    /// Occurs after a catalog is successfully initialized or replaced.
    /// </summary>
    public static event Action? CatalogChanged;

    /// <summary>
    /// Occurs after the current locale changes.
    /// </summary>
    public static event Action? LocaleChanged;

    /// <summary>
    /// Gets a value indicating whether a localization catalog has been initialized.
    /// </summary>
    public static bool IsInitialized => _runtime != null;

    /// <summary>
    /// Gets the locales declared by the active catalog in display order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when localization has not been initialized.</exception>
    public static IReadOnlyList<I18nRuntimeLocale> Locales => Runtime.Locales;

    /// <summary>
    /// Gets the currently selected locale.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when localization has not been initialized.</exception>
    public static I18nRuntimeLocale CurrentLocale => Runtime.CurrentLocale;

    private static I18nRuntime Runtime => _runtime ?? throw new InvalidOperationException(
        "Localization has not been initialized. Call i18n.Initialize before using it.");

    /// <summary>
    /// Builds a runtime snapshot from a Unity catalog asset.
    /// </summary>
    /// <param name="catalogAsset">Catalog asset whose JSON source will be loaded.</param>
    /// <param name="localeId">
    /// Initial locale identifier, or <see langword="null"/> to use the catalog default locale.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogAsset"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the catalog has no JSON source.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="localeId"/> is not declared by the catalog.</exception>
    /// <exception cref="I18nInvalidCatalogException">Thrown when the source catalog is invalid.</exception>
    public static void Initialize(I18nCatalogAsset catalogAsset, string? localeId = null)
    {
        if (!catalogAsset)
        {
            throw new ArgumentNullException(nameof(catalogAsset));
        }

        if (!catalogAsset.SourceCatalog)
        {
            throw new InvalidOperationException(
                $"Localization catalog asset '{catalogAsset.name}' has no source JSON.");
        }

        I18nCatalog catalog = catalogAsset.Deserialize();
        I18nRuntime runtime = localeId == null
            ? new I18nRuntime(catalog)
            : new I18nRuntime(catalog, localeId);

        _runtime = runtime;
        _hasWarnedAboutNone = false;
        CatalogChanged?.Invoke();
    }

    /// <summary>
    /// Gets localized text for a stable entry ID.
    /// </summary>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <returns>The resolved localized text or <see cref="NonePlaceholder"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when localization has not been initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    public static string Text(long id)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id);
    }

    /// <summary>
    /// Formats localized text using the current locale culture.
    /// </summary>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <param name="arguments">Values inserted into the localized composite format string.</param>
    /// <returns>The formatted localized text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when localization has not been initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
    public static string Format(long id, params object?[] arguments)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Format(id, arguments);
    }

    /// <summary>
    /// Changes the active locale.
    /// </summary>
    /// <param name="localeId">Declared locale identifier to select.</param>
    /// <returns><see langword="true"/> when the locale changed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when localization has not been initialized.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localeId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the locale is not declared by the catalog.</exception>
    public static bool SetLocale(string localeId)
    {
        if (!Runtime.SetLocale(localeId))
        {
            return false;
        }

        LocaleChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Resets static runtime state before application startup and every Play Mode session.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _runtime = null;
        _hasWarnedAboutNone = false;
        CatalogChanged = null;
        LocaleChanged = null;
    }

    private static void WarnAboutNone()
    {
        if (_hasWarnedAboutNone)
        {
            return;
        }

        _hasWarnedAboutNone = true;
        Debug.LogWarning(
            $"An unassigned localization key was requested. Returning '{NonePlaceholder}'.");
    }
}
