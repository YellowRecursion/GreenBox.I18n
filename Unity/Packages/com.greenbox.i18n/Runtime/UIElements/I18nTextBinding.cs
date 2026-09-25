#nullable enable

using System;
using GreenBox.I18n;
using Unity.Properties;
using UnityEngine.UIElements;

namespace GreenBox.I18n.Unity.UIElements
{
    /// <summary>
    /// Binds a UI Toolkit text property to a GreenBox catalog entry.
    /// </summary>
    [UxmlObject]
    public partial class I18nTextBinding : CustomBinding
    {
        private long _entryId;
        private int _activationCount;

        /// <summary>
        /// Gets or sets the stable ID of the text entry.
        /// </summary>
        [UxmlAttribute("entry-id")]
        public long EntryId
        {
            get => _entryId;
            set
            {
                if (_entryId == value)
                {
                    return;
                }

                _entryId = value;
                MarkDirty();
            }
        }

        public I18nTextBinding()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        protected override void OnActivated(in BindingActivationContext context)
        {
            if (_activationCount++ == 0)
            {
                global::I18n.LocaleChanged += HandleLocaleChanged;
            }

            MarkDirty();
        }

        protected override void OnDeactivated(in BindingActivationContext context)
        {
            if (--_activationCount == 0)
            {
                global::I18n.LocaleChanged -= HandleLocaleChanged;
            }
        }

        protected override BindingResult Update(in BindingContext context)
        {
            try
            {
                string value = global::I18n.Text(_entryId);
                VisualElement element = context.targetElement;
                if (ConverterGroups.TrySetValueGlobal(
                        ref element, context.bindingId, value, out VisitReturnCode errorCode))
                {
                    return new BindingResult(BindingStatus.Success);
                }

                return new BindingResult(
                    BindingStatus.Failure,
                    $"GreenBox text binding cannot write '{context.bindingId}': {errorCode}.");
            }
            catch (Exception exception)
            {
                return new BindingResult(BindingStatus.Failure, exception.Message);
            }
        }

        private void HandleLocaleChanged(I18nRuntimeLocale _)
        {
            MarkDirty();
        }
    }
}
