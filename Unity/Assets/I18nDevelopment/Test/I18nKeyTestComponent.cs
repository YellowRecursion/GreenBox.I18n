using GreenBox.I18n.Unity;
using UnityEngine;

namespace GreenBox.I18n.Development
{
    /// <summary>
    /// Provides assigned, unassigned, and missing localization keys for editor testing.
    /// </summary>
    [AddComponentMenu("GreenBox/i18n/Development/Key Test")]
    public sealed class I18nKeyTestComponent : MonoBehaviour
    {
        /// <summary>
        /// An unassigned localization key.
        /// </summary>
        public I18nKey None;

        /// <summary>
        /// A localization key present in the development catalog.
        /// </summary>
        public I18nKey Existing = new I18nKey(3857341477483323829);

        /// <summary>
        /// A localization key intentionally absent from the development catalog.
        /// </summary>
        public I18nKey Missing = new I18nKey(3857602839270400377);

        private void Awake()
        {
            var str = global::I18n.Text(3857404068572302532);
            var unknown = global::I18n.Text(3857535036959804040);
            var unknown2 = global::I18n.Text(3857535036959804040);
        }

        /// <summary>
        /// Logs the existing key in English and Russian.
        /// </summary>
        [ContextMenu("Log Runtime Text")]
        private void LogRuntimeText()
        {
            Debug.Log(global::I18n.Text(Existing), this);

            global::I18n.SetLocale("ru");
            Debug.Log(global::I18n.Text(Existing), this);

            TextAsset asset = global::I18n.Asset<TextAsset>(Existing);
            Debug.Log($"Resolved asset: {asset.name}", asset);
        }
    }
}
