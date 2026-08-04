#nullable enable

using GreenBox.I18n;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Inspectors
{
    /// <summary>
    /// Draws source, compilation state, controls, and diagnostics for a Unity catalog asset.
    /// </summary>
    [CustomEditor(typeof(I18nCatalogAsset))]
    internal sealed class I18nCatalogAssetInspector : UnityEditor.Editor
    {
        private const string SourceCatalogPropertyName = "_sourceCatalog";

        private SerializedProperty? _sourceCatalogProperty;
        private I18nCatalogCompilationResult? _lastCompilationResult;

        private I18nCatalogAsset CatalogAsset => (I18nCatalogAsset)target;

        private void OnEnable()
        {
            _sourceCatalogProperty = serializedObject.FindProperty(SourceCatalogPropertyName);
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceCatalogProperty);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _lastCompilationResult = null;
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            DrawCompilationState();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!CatalogAsset.SourceCatalog))
            {
                if (GUILayout.Button("Compile Catalog"))
                {
                    _lastCompilationResult = I18nCatalogCompiler.Compile(CatalogAsset);
                }
            }

            DrawDiagnostics();
        }

        private void DrawCompilationState()
        {
            if (_lastCompilationResult != null && !_lastCompilationResult.IsSuccess)
            {
                EditorGUILayout.HelpBox("Compilation failed.", MessageType.Error);
                return;
            }

            I18nCatalogCompilationState state = I18nCatalogCompiler.GetState(CatalogAsset);
            switch (state)
            {
                case I18nCatalogCompilationState.NotCompiled:
                    EditorGUILayout.HelpBox("Catalog is not compiled.", MessageType.Warning);
                    break;
                case I18nCatalogCompilationState.OutOfDate:
                    EditorGUILayout.HelpBox("Compiled data is out of date.", MessageType.Warning);
                    break;
                case I18nCatalogCompilationState.UpToDate:
                    EditorGUILayout.HelpBox(
                        $"Catalog is up to date. Compiled asset bindings: {CatalogAsset.AssetBindings.Count}.",
                        MessageType.Info);
                    break;
            }
        }

        private void DrawDiagnostics()
        {
            if (_lastCompilationResult == null)
            {
                return;
            }

            I18nValidationResult? validationResult = _lastCompilationResult.ValidationResult;
            if (validationResult != null)
            {
                for (int diagnosticIndex = 0;
                     diagnosticIndex < validationResult.Diagnostics.Count;
                     diagnosticIndex++)
                {
                    I18nValidationDiagnostic diagnostic = validationResult.Diagnostics[diagnosticIndex];
                    MessageType messageType = diagnostic.Severity == I18nValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning;
                    DrawDiagnostic(
                        diagnostic.Code,
                        diagnostic.JsonPath,
                        diagnostic.Message,
                        messageType);
                }
            }

            for (int errorIndex = 0; errorIndex < _lastCompilationResult.Errors.Count; errorIndex++)
            {
                I18nCatalogCompilationError error = _lastCompilationResult.Errors[errorIndex];
                DrawDiagnostic(error.Code, error.JsonPath, error.Message, MessageType.Error);
            }
        }

        private static void DrawDiagnostic(
            string code,
            string jsonPath,
            string message,
            MessageType messageType)
        {
            EditorGUILayout.HelpBox(
                $"{code}\n{jsonPath}\n{message}",
                messageType);
        }
    }
}
