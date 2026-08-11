#nullable enable

using System;
using System.Collections.Generic;
using GreenBox.I18n;
using GreenBox.I18n.Unity;
using GreenBox.I18n.Unity.Assets;
using UnityEngine;

/// <summary>
/// Provides short, project-wide access to the active Unity localization runtime.
/// </summary>
public static class I18n
{
    private const string LocaleOverridePlayerPrefsKey =
        "com.greenbox.i18n.locale-override";

    /// <summary>
    /// Text returned for an unassigned localization key.
    /// </summary>
    public const string NonePlaceholder = "[none]";

    private static I18nRuntime? _runtime;
    private static I18nUnityAssetResolver? _assetResolver;
    private static Exception? _loadException;
    private static bool _isLoading;
    private static bool _hasWarnedAboutNone;

    /// <summary>
    /// Occurs after the current locale changes.
    /// </summary>
    public static event Action<I18nRuntimeLocale>? LocaleChanged;

    /// <summary>
    /// Gets a value indicating whether the localization catalog has been loaded.
    /// </summary>
    public static bool IsInitialized => _runtime != null;

    /// <summary>
    /// Gets the locales declared by the active catalog in display order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the project catalog cannot be loaded.</exception>
    public static IReadOnlyList<I18nRuntimeLocale> Locales => Runtime.Locales;

    /// <summary>
    /// Gets the currently selected locale.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the project catalog cannot be loaded.</exception>
    public static I18nRuntimeLocale CurrentLocale => Runtime.CurrentLocale;

    private static I18nRuntime Runtime
    {
        get
        {
            EnsureLoaded();
            return _runtime!;
        }
    }

    private static I18nUnityAssetResolver AssetResolver
    {
        get
        {
            EnsureLoaded();
            return _assetResolver!;
        }
    }

    /// <summary>
    /// Loads the project catalog before the first scene starts.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadBeforeFirstScene()
    {
        try
        {
            EnsureLoaded();
        }
        catch (Exception exception)
        {
            Debug.LogError("[i18n] Automatic catalog loading failed. " + exception.Message);
        }
    }

    private static void EnsureLoaded()
    {
        if (_runtime != null)
        {
            return;
        }

        if (_loadException != null)
        {
            throw new InvalidOperationException(
                "The GreenBox I18n project catalog could not be loaded.",
                _loadException);
        }

        if (_isLoading)
        {
            throw new InvalidOperationException(
                "Recursive GreenBox I18n catalog loading was detected.");
        }

        _isLoading = true;
        try
        {
            I18nCatalogAsset? catalogAsset =
                Resources.Load<I18nCatalogAsset>(I18nCatalogAsset.ResourcesPath);
            if (!catalogAsset)
            {
                throw new InvalidOperationException(
                    $"Catalog resource '{I18nCatalogAsset.ResourcesPath}' was not found. " +
                    "Open the Unity project once to let GreenBox I18n repair its project files.");
            }

            if (!catalogAsset.HasCompiledCatalog)
            {
                throw new InvalidOperationException(
                    $"Localization catalog asset '{catalogAsset.name}' has not been compiled. " +
                    "Open the Unity project once to regenerate it.");
            }

            I18nCompiledCatalog catalog = catalogAsset.DeserializeCompiledCatalog();
            var runtime = new I18nRuntime(catalog);
            ApplyInitialLocale(runtime);
            var assetResolver = new I18nUnityAssetResolver(catalogAsset.AssetBindings);

            _runtime = runtime;
            _assetResolver = assetResolver;
            _hasWarnedAboutNone = false;
        }
        catch (Exception exception)
        {
            _loadException = exception;
            throw;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Gets localized text for a stable entry ID.
    /// </summary>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <returns>The resolved localized text or <see cref="NonePlaceholder"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the project catalog cannot be loaded.</exception>
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

    /// <summary>Gets and formats localized text with one named argument.</summary>
    public static string Text<T1>(long id, (string Name, T1 Value) argument1)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1);
    }

    /// <summary>Gets and formats localized text with two named arguments.</summary>
    public static string Text<T1, T2>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2);
    }

    /// <summary>Gets and formats localized text with 3 named arguments.</summary>
    public static string Text<T1, T2, T3>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3);
    }

    /// <summary>Gets and formats localized text with 4 named arguments.</summary>
    public static string Text<T1, T2, T3, T4>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3,
        (string Name, T4 Value) argument4)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3, argument4);
    }

    /// <summary>Gets and formats localized text with 5 named arguments.</summary>
    public static string Text<T1, T2, T3, T4, T5>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3,
        (string Name, T4 Value) argument4,
        (string Name, T5 Value) argument5)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3, argument4, argument5);
    }

    /// <summary>Gets and formats localized text with 6 named arguments.</summary>
    public static string Text<T1, T2, T3, T4, T5, T6>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3,
        (string Name, T4 Value) argument4,
        (string Name, T5 Value) argument5,
        (string Name, T6 Value) argument6)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3, argument4, argument5, argument6);
    }

    /// <summary>Gets and formats localized text with 7 named arguments.</summary>
    public static string Text<T1, T2, T3, T4, T5, T6, T7>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3,
        (string Name, T4 Value) argument4,
        (string Name, T5 Value) argument5,
        (string Name, T6 Value) argument6,
        (string Name, T7 Value) argument7)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3, argument4, argument5, argument6, argument7);
    }

    /// <summary>Gets and formats localized text with 8 named arguments.</summary>
    public static string Text<T1, T2, T3, T4, T5, T6, T7, T8>(
        long id,
        (string Name, T1 Value) argument1,
        (string Name, T2 Value) argument2,
        (string Name, T3 Value) argument3,
        (string Name, T4 Value) argument4,
        (string Name, T5 Value) argument5,
        (string Name, T6 Value) argument6,
        (string Name, T7 Value) argument7,
        (string Name, T8 Value) argument8)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8);
    }

    /// <summary>Gets and formats localized text with an uncommon number of named arguments.</summary>
    public static string Text(long id, params (string Name, object? Value)[] arguments)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return NonePlaceholder;
        }

        return Runtime.Text(id, arguments);
    }

    /// <summary>
    /// Gets the localized Unity object through the current locale fallback chain.
    /// </summary>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <returns>The resolved Unity object, or <see langword="null"/> when no asset is assigned.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project catalog cannot be loaded or was not compiled.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    public static UnityEngine.Object? Asset(long id)
    {
        if (id == 0)
        {
            WarnAboutNone();
            return null;
        }

        I18nAssetReference? reference = Runtime.Asset(id);
        return reference == null ? null : AssetResolver.Resolve(reference);
    }

    /// <summary>
    /// Gets a localized Unity object of the requested type.
    /// </summary>
    /// <typeparam name="T">Expected Unity object type.</typeparam>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <returns>The resolved object, or <see langword="null"/> when no asset is assigned.</returns>
    /// <exception cref="InvalidCastException">Thrown when the assigned object has an incompatible type.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project catalog cannot be loaded or was not compiled.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    public static T? Asset<T>(long id)
        where T : UnityEngine.Object
    {
        UnityEngine.Object? asset = Asset(id);
        if (!asset)
        {
            return null;
        }

        if (asset is T typedAsset)
        {
            return typedAsset;
        }

        throw new InvalidCastException(
            $"Localization asset for entry ID '{id}' is '{asset.GetType().FullName}', " +
            $"not '{typeof(T).FullName}'.");
    }

    /// <summary>
    /// Attempts to get a localized Unity object of the requested type.
    /// </summary>
    /// <typeparam name="T">Expected Unity object type.</typeparam>
    /// <param name="id">Positive entry ID, or zero for an unassigned key.</param>
    /// <param name="asset">Receives the compatible resolved object.</param>
    /// <returns>
    /// <see langword="true"/> when a compatible asset is assigned; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project catalog cannot be loaded or was not compiled.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    public static bool TryGetAsset<T>(long id, out T? asset)
        where T : UnityEngine.Object
    {
        UnityEngine.Object? resolvedAsset = Asset(id);
        asset = resolvedAsset as T;
        return asset;
    }

    /// <summary>
    /// Changes the active locale.
    /// </summary>
    /// <param name="localeId">Declared locale identifier to select.</param>
    /// <returns><see langword="true"/> when the locale changed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the project catalog cannot be loaded.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localeId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the locale is not declared by the catalog.</exception>
    public static bool SetLocale(string localeId)
    {
        I18nRuntime runtime = Runtime;
        bool changed = runtime.SetLocale(localeId);
        PlayerPrefs.SetString(LocaleOverridePlayerPrefsKey, localeId);

        if (changed)
        {
            LocaleChanged?.Invoke(runtime.CurrentLocale);
        }

        return changed;
    }

    /// <summary>
    /// Removes the saved locale override and selects the best locale for the current device.
    /// </summary>
    /// <returns><see langword="true"/> when the active locale changed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the project catalog cannot be loaded.</exception>
    public static bool ClearLocaleOverride()
    {
        PlayerPrefs.DeleteKey(LocaleOverridePlayerPrefsKey);
        I18nRuntime runtime = Runtime;

        string localeId = I18nLocaleSelector.SelectDeviceLocale(
            runtime.Locales,
            runtime.DefaultLocale.Id);
        bool changed = runtime.SetLocale(localeId);
        if (changed)
        {
            LocaleChanged?.Invoke(runtime.CurrentLocale);
        }

        return changed;
    }

    /// <summary>
    /// Resets static runtime state before application startup and every Play Mode session.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _runtime = null;
        _assetResolver = null;
        _loadException = null;
        _isLoading = false;
        _hasWarnedAboutNone = false;
        LocaleChanged = null;
    }

    private static void ApplyInitialLocale(I18nRuntime runtime)
    {
        if (PlayerPrefs.HasKey(LocaleOverridePlayerPrefsKey))
        {
            string localeId = PlayerPrefs.GetString(LocaleOverridePlayerPrefsKey);
            if (ContainsLocale(runtime.Locales, localeId))
            {
                runtime.SetLocale(localeId);
                return;
            }

            PlayerPrefs.DeleteKey(LocaleOverridePlayerPrefsKey);
        }

        runtime.SetLocale(I18nLocaleSelector.SelectDeviceLocale(
            runtime.Locales,
            runtime.DefaultLocale.Id));
    }

    private static bool ContainsLocale(
        IReadOnlyList<I18nRuntimeLocale> locales,
        string localeId)
    {
        for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
        {
            if (string.Equals(
                    locales[localeIndex].Id,
                    localeId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
