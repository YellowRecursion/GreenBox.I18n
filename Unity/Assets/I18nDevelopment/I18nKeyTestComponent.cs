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
    }
}
