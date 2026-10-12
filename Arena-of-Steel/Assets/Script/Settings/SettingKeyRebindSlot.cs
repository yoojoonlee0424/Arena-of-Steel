using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingKeyRebindSlot : MonoBehaviour
{
    [Header("UI 바인딩")]
    [SerializeField] private TextMeshProUGUI actionNameText;
    [SerializeField] private TextMeshProUGUI keyNameText;
    [SerializeField] private Button rebindButton;

    [Header("설정 정보")]
    [Tooltip("액션 경로 (예: Player/Move)")]
    [SerializeField] private string actionPath = "";
    [Tooltip("컴포지트 파트 (예: up)")]
    [SerializeField] private string compositePart = "";
    [SerializeField] private string displayName = "";

    private InputAction targetAction;
    private int bindingIndex = -1;
    private bool isRebinding;

    private void Awake()
    {
        AutoFind();

        if (rebindButton != null)
        {
            rebindButton.onClick.AddListener(StartRebinding);
        }
    }

    private void Reset()
    {
        AutoFind();
    }

    public void Init(InputActionAsset asset)
    {
        if (asset != null && !string.IsNullOrEmpty(actionPath))
        {
            targetAction = asset.FindAction(actionPath);
        }
        else
        {
            targetAction = null;
        }

        bindingIndex = FindBindingIndex(targetAction, compositePart);

        if (actionNameText != null)
        {
            actionNameText.text = displayName;
        }

        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (keyNameText == null) return;

        if (!HasValidBinding())
        {
            keyNameText.text = "-";
            return;
        }

        string display = targetAction.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        if (string.IsNullOrEmpty(display))
            display = targetAction.bindings[bindingIndex].ToDisplayString();

        keyNameText.text = string.IsNullOrEmpty(display) ? "-" : display;
    }

    public void StartRebinding()
    {
        if (isRebinding || SettingManager.Instance == null || !HasValidBinding()) return;

        isRebinding = true;

        if (keyNameText != null)
        {
            keyNameText.text = "[ Input Waitng... ]";
        }

        if (rebindButton != null)
        {
            rebindButton.interactable = false;
        }

        SettingManager.Instance.Rebind(targetAction, bindingIndex, OnRebindFinished);
    }

    private void OnRebindFinished()
    {
        isRebinding = false;

        if (rebindButton != null)
        {
            rebindButton.interactable = true;
        }

        RefreshDisplay();
    }

    private bool HasValidBinding()
    {
        if (targetAction == null) return false;

        if (bindingIndex < 0 || bindingIndex >= targetAction.bindings.Count)
        {
            bindingIndex = FindBindingIndex(targetAction, compositePart);
        }

        return bindingIndex >= 0 && bindingIndex < targetAction.bindings.Count;
    }

    private static int FindBindingIndex(InputAction action, string part)
    {
        if (action == null) return -1;

        var bindings = action.bindings;
        bool hasPart = !string.IsNullOrEmpty(part);

        if (hasPart)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isPartOfComposite && bindings[i].name.Equals(part, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        int fallback = -1;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;

            if ((b.groups != null && b.groups.Contains("Keyboard")) ||
                (b.effectivePath != null && b.effectivePath.Contains("Keyboard")))
            {
                return i;
            }

            if (fallback < 0)
            {
                fallback = i;
            }
        }

        return fallback;
    }

    private void AutoFind()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            string tName = texts[i].gameObject.name.ToLower();
            if (actionNameText == null && (tName.Contains("action") || tName.Contains("title") || tName.Contains("label") || tName.Contains("name")))
            {
                actionNameText = texts[i];
            }
            else if (keyNameText == null && (tName.Contains("key") || tName.Contains("bind") || tName.Contains("value") || tName.Contains("button")))
            {
                keyNameText = texts[i];
            }
        }

        if (rebindButton == null)
        {
            rebindButton = GetComponentInChildren<Button>(true);
        }
    }
}
