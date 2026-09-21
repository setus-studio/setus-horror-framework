using Setus.HorrorFramework.Localization;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Localization
{
    [CustomEditor(typeof(LocaleFontProfile))]
    public sealed class LocaleFontProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Use Authored Font only when every UI font contains the locale's required glyphs. " +
                "Otherwise assign a game-owned font imported under Assets/Game/Localization/Fonts.",
                MessageType.Info);

            var bindings = serializedObject.FindProperty("bindings");
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                var code = binding.FindPropertyRelative("localeCode");
                var authored = binding.FindPropertyRelative("useAuthoredFont");
                var font = binding.FindPropertyRelative("fontOverride");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(code.stringValue)
                    ? $"Locale {index}"
                    : code.stringValue, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(authored, new GUIContent("Use Authored Font"));
                using (new EditorGUI.DisabledScope(authored.boolValue))
                {
                    EditorGUILayout.PropertyField(font, new GUIContent("Font Override"));
                }
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
