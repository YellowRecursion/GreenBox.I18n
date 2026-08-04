using GreenBox.I18n.Unity;
using UnityEngine;

namespace GreenBox.I18n.Development
{
    /// <summary>
    /// Provides assigned, unassigned, and missing localization keys for editor testing.
    /// </summary>
    [AddComponentMenu("GreenBox/I18n Key Test Component")]
    public sealed class I18nKeyTestComponent : MonoBehaviour
    {
        /// <summary>
        /// An unassigned localization key.
        /// </summary>
        public I18nKey None;

        /// <summary>
        /// A localization key present in the development catalog.
        /// </summary>
        public I18nKey Existing = new I18nKey(1001);

        /// <summary>
        /// A localization key intentionally absent from the development catalog.
        /// </summary>
        public I18nKey Missing = new I18nKey(999999);

        /// <summary>
        /// Catalog used by the runtime smoke test.
        /// </summary>
        public I18nCatalogAsset Catalog;

        /// <summary>
        /// Initializes the runtime and logs the existing key in English and Russian.
        /// </summary>
        [ContextMenu("Log Runtime Text")]
        private void LogRuntimeText()
        {
            i18n.Initialize(Catalog, "en");
            Debug.Log(i18n.Text(Existing), this);

            i18n.SetLocale("ru");
            Debug.Log(i18n.Text(Existing), this);
        }
    }
}
