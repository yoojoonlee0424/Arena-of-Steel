#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[CustomEditor(typeof(SettingKeyRebindSlot))]
public class SettingKeyRebindSlotEditor : Editor
{
    private SerializedProperty actionReferenceProp;
    private SerializedProperty bindingIdProp;
    private SerializedProperty displayNameProp;
    private SerializedProperty actionNameTextProp;
    private SerializedProperty keyNameTextProp;
    private SerializedProperty rebindButtonProp;

    private void OnEnable()
    {
        actionReferenceProp = serializedObject.FindProperty("actionReference");
        bindingIdProp = serializedObject.FindProperty("bindingId");
        displayNameProp = serializedObject.FindProperty("displayName");
        actionNameTextProp = serializedObject.FindProperty("actionNameText");
        keyNameTextProp = serializedObject.FindProperty("keyNameText");
        rebindButtonProp = serializedObject.FindProperty("rebindButton");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(actionReferenceProp, new GUIContent("Action Reference"));

        var actionRef = actionReferenceProp.objectReferenceValue as InputActionReference;
        if (actionRef != null && actionRef.action != null)
        {
            var action = actionRef.action;
            var bindings = action.bindings;

            List<GUIContent> options = new List<GUIContent>();
            List<string> ids = new List<string>();

            int selectedIndex = -1;
            string currentId = bindingIdProp.stringValue;

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite) continue;

                string idStr = b.id.ToString();
                string partPrefix = b.isPartOfComposite ? $"{b.name.ToUpper()}: " : "";
                string display = $"{partPrefix}{b.ToDisplayString()}";
                string group = !string.IsNullOrEmpty(b.groups) ? $" [{b.groups.Replace(";", "")}]" : "";

                options.Add(new GUIContent($"{display}{group}"));
                ids.Add(idStr);

                if (!string.IsNullOrEmpty(currentId) && idStr.Equals(currentId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = options.Count - 1;
                }
            }

            if (options.Count > 0)
            {
                if (selectedIndex < 0) selectedIndex = 0;

                int newIndex = EditorGUILayout.Popup(new GUIContent("Target Binding"), selectedIndex, options.ToArray());
                if (newIndex >= 0 && newIndex < ids.Count)
                {
                    bindingIdProp.stringValue = ids[newIndex];
                }
            }
            else
            {
                EditorGUILayout.HelpBox("선택된 액션에 유효한 바인딩이 없습니다.", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Action Reference를 먼저 지정해 주세요.", MessageType.Info);
        }

        EditorGUILayout.PropertyField(displayNameProp, new GUIContent("Display Name"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI 컴포넌트", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actionNameTextProp);
        EditorGUILayout.PropertyField(keyNameTextProp);
        EditorGUILayout.PropertyField(rebindButtonProp);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
