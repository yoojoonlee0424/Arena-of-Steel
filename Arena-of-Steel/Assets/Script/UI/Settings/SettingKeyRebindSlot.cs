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
    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private string actionName;
    [SerializeField] private string bindingId;
    [SerializeField] private string displayName;

    private InputAction targetAction;
    private int bindingIndex = -1;
    private bool isRebinding;

    public InputActionReference ActionReference => actionReference;
    public string BindingId => bindingId;

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
        if (actionReference != null && actionReference.action != null)
        {
            targetAction = asset != null ? asset.FindAction(actionReference.action.id) : actionReference.action;
        }
        else if (asset != null && !string.IsNullOrEmpty(actionName))
        {
            targetAction = asset.FindAction(actionName);
        }
        else
        {
            targetAction = null;
        }

        bindingIndex = FindBindingIndex(targetAction, bindingId);

        if (actionNameText != null)
        {
            actionNameText.text = !string.IsNullOrEmpty(displayName)
                ? displayName
                : (targetAction != null ? targetAction.name : "");
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
            bindingIndex = FindBindingIndex(targetAction, bindingId);
        }

        return bindingIndex >= 0 && bindingIndex < targetAction.bindings.Count;
    }

    private static int FindBindingIndex(InputAction action, string idString)
    {
        if (action == null) return -1;

        if (!string.IsNullOrEmpty(idString) && Guid.TryParse(idString, out var targetId))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id == targetId)
                {
                    return i;
                }
            }
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
            {
                return i;
            }
        }

        return -1;
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
