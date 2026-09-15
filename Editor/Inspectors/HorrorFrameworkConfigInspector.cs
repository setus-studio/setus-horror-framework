using Setus.HorrorFramework.Core.Config;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Inspectors
{
    [CustomEditor(typeof(HorrorFrameworkConfig))]
    public sealed class HorrorFrameworkConfigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var config = (HorrorFrameworkConfig)target;
            if (config.DebugEnabledByDefault)
            {
                EditorGUILayout.HelpBox(
                    "Debug is enabled by default. Disable it for production builds unless this is an intentional development configuration.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Production-safe debug default: OFF.", MessageType.Info);
            }
        }
    }
}
