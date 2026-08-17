using System.Collections.Generic;
using GreenBox.I18n.Unity;
using NUnit.Framework;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nComponentEditModeTests
    {
        [Test]
        public void AddingComponent_WithUnassignedKey_DoesNotWarnAndStillAppliesPlaceholder()
        {
            var warnings = new List<string>();
            var gameObject = new GameObject("Unassigned I18n component");

            void CaptureWarning(string condition, string _, LogType type)
            {
                if (type == LogType.Warning &&
                    condition.Contains("An unassigned localization key was requested"))
                {
                    warnings.Add(condition);
                }
            }

            Application.logMessageReceived += CaptureWarning;
            try
            {
                TestI18nComponent component = gameObject.AddComponent<TestI18nComponent>();

                Assert.That(component.Content, Is.EqualTo(global::I18n.NonePlaceholder));
                Assert.That(warnings, Is.Empty);
            }
            finally
            {
                Application.logMessageReceived -= CaptureWarning;
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class TestI18nComponent : I18nComponent
        {
            public string Content { get; private set; }

            protected override void UpdateContent()
            {
                Content = global::I18n.Text(Key);
            }
        }
    }
}
