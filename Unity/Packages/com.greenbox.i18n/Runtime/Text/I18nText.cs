#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LegacyText = UnityEngine.UI.Text;

namespace GreenBox.I18n.Unity.Text
{
    /// <summary>
    /// Keeps a TextMeshPro or legacy Unity UI text component synchronized with localization.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("GreenBox/i18n/I18n Text")]
    [Icon("Packages/com.greenbox.i18n/Editor/Assets/ComponentIcons/GreenBox.I18n.png")]
    public sealed class I18nText : I18nComponent
    {
        [SerializeField]
        private I18nTextTransform _textTransform;

        private Graphic? _target;
        private bool _hasReportedMissingTarget;

        /// <summary>
        /// Gets or sets the presentation transformation applied to localized text.
        /// </summary>
        public I18nTextTransform TextTransform
        {
            get => _textTransform;
            set
            {
                if (_textTransform == value)
                {
                    return;
                }

                _textTransform = value;
                Refresh();
            }
        }

        /// <summary>
        /// Gets the attached TextMeshPro or legacy Unity UI text as their common graphic type.
        /// </summary>
        public Graphic? Target
        {
            get
            {
                if (!_target)
                {
                    CacheTarget();
                }

                return _target;
            }
        }

        /// <summary>
        /// Gets the text currently displayed by the target, or <see langword="null"/> when no target exists.
        /// </summary>
        public string? Text => Target switch
        {
            TMP_Text textMeshPro => textMeshPro.text,
            LegacyText legacyText => legacyText.text,
            _ => null
        };

        /// <summary>
        /// Resolves, transforms, and applies the current localized text.
        /// </summary>
        protected override void UpdateContent()
        {
            Graphic? target = Target;
            if (!target)
            {
                ReportMissingTarget();
                return;
            }

            string localizedText = global::I18n.NonePlaceholder;
            if (Key.IsAssigned)
            {
                localizedText = global::I18n.Text(Key);
                localizedText = I18nTextTransformUtility.Apply(
                    localizedText,
                    _textTransform,
                    global::I18n.CurrentLocale.Culture);
            }

            switch (target)
            {
                case TMP_Text textMeshPro:
                    textMeshPro.text = localizedText;
                    break;
                case LegacyText legacyText:
                    legacyText.text = localizedText;
                    break;
            }
        }

        private void Awake()
        {
            CacheTarget();
        }

        private void CacheTarget()
        {
            TMP_Text? textMeshPro = GetComponent<TMP_Text>();
            if (textMeshPro)
            {
                _target = textMeshPro;
                return;
            }

            _target = GetComponent<LegacyText>();
        }

        private void ReportMissingTarget()
        {
            if (_hasReportedMissingTarget)
            {
                return;
            }

            _hasReportedMissingTarget = true;
            Debug.LogError(
                "I18nText requires a TextMeshPro or UnityEngine.UI.Text component on the same GameObject.",
                this);
        }
    }
}
