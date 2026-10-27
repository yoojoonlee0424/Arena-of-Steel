using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public class TMPFontReplacerWindow : EditorWindow
{
    private const string DefaultKoreanFontGuid = "8f586378b4e144a9851e7b34d9b748ee";
    private const string DefaultKoreanFontPath = "Assets/Fonts/NotoSans-VariableFont_wdth,wght SDF.asset";

    [SerializeField] private TMP_FontAsset targetFont;
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private bool includeInactive = true;

    [MenuItem("Tools/Font/TMP Font Replacer Window")]
    public static void Open()
    {
        var window = GetWindow<TMPFontReplacerWindow>("Font Replacer");
        window.minSize = new Vector2(350, 200);
        window.Show();
    }

    [MenuItem("GameObject/Replace TMP Fonts in Children", false, 25)]
    public static void ReplaceFromContextMenu()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("알림", "Hierarchy 또는 Project 창에서 대상 GameObject를 선택해주세요.", "확인");
            return;
        }

        var font = LoadDefaultKoreanFont();
        if (font == null)
        {
            Open();
            return;
        }

        int count = ReplaceFontsInHierarchy(selected, font, true);
        EditorUtility.DisplayDialog("완료", $"'{selected.name}' 및 자식 오브젝트의 TMP 텍스트 {count}개의 폰트를 '{font.name}'으로 교체했습니다.", "확인");
    }

    private void OnEnable()
    {
        if (targetFont == null)
            targetFont = LoadDefaultKoreanFont();

        if (targetRoot == null)
            targetRoot = Selection.activeGameObject;
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            targetRoot = Selection.activeGameObject;
            Repaint();
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("TMP 폰트 일괄 교체기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("선택한 GameObject와 모든 자식의 TextMeshPro 폰트를 지정한 폰트로 일괄 교체합니다.", MessageType.Info);
        GUILayout.Space(10);

        targetRoot = (GameObject)EditorGUILayout.ObjectField("대상 루트 오브젝트", targetRoot, typeof(GameObject), true);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("변경할 TMP 폰트", targetFont, typeof(TMP_FontAsset), false);
        includeInactive = EditorGUILayout.Toggle("비활성 오브젝트 포함", includeInactive);

        GUILayout.Space(15);

        GUI.enabled = targetRoot != null && targetFont != null;
        if (GUILayout.Button("폰트 일괄 교체 실행", GUILayout.Height(35)))
        {
            int count = ReplaceFontsInHierarchy(targetRoot, targetFont, includeInactive);
            EditorUtility.DisplayDialog("완료", $"{count}개의 TextMeshPro 텍스트 폰트를 '{targetFont.name}'으로 교체했습니다.", "확인");
        }
        GUI.enabled = true;
    }

    public static int ReplaceFontsInHierarchy(GameObject root, TMP_FontAsset newFont, bool includeInactive)
    {
        if (root == null || newFont == null) return 0;

        var texts = root.GetComponentsInChildren<TMP_Text>(includeInactive);
        int changedCount = 0;

        foreach (var text in texts)
        {
            if (text == null) continue;

            Undo.RecordObject(text, "Replace TMP Font");
            text.font = newFont;
            if (newFont.material != null)
                text.fontSharedMaterial = newFont.material;

            EditorUtility.SetDirty(text);
            changedCount++;
        }

        if (PrefabUtility.IsPartOfAnyPrefab(root))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                EditorUtility.SetDirty(stage.prefabContentsRoot);
            }
        }

        Debug.Log($"[TMPFontReplacer] '{root.name}' 내부 {changedCount}개의 TMP_Text 폰트를 '{newFont.name}'으로 변경했습니다.");
        return changedCount;
    }

    private static TMP_FontAsset LoadDefaultKoreanFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultKoreanFontPath);
        if (font != null) return font;

        string path = AssetDatabase.GUIDToAssetPath(DefaultKoreanFontGuid);
        if (!string.IsNullOrEmpty(path))
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        return null;
    }
}
