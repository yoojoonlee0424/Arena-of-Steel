using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingSelectorControl : MonoBehaviour
{
    [Header("UI 바인딩")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("설정 정보")]
    [SerializeField] private string titleName = "";
    [SerializeField] private bool loop = true;

    public int CurrentIndex { get; private set; }
    public event Action<int> onIndexChanged;

    private readonly List<string> options = new();

    private void Awake()
    {
        AutoFind();
        BindEvents();
    }

    private void Reset() => AutoFind();

    public void Init(List<string> newOptions, int defaultIndex = 0)
    {
        options.Clear();
        if (newOptions != null) options.AddRange(newOptions);

        if (!string.IsNullOrEmpty(titleName) && titleText != null)
            titleText.text = titleName;

        SetIndex(defaultIndex, notify: false);
    }

    public void SetIndex(int index, bool notify = true)
    {
        if (options.Count == 0)
        {
            CurrentIndex = 0;
            if (valueText != null) valueText.text = "";
            return;
        }

        CurrentIndex = Mathf.Clamp(index, 0, options.Count - 1);

        if (valueText != null)
            valueText.text = options[CurrentIndex];

        UpdateButtons();

        if (notify)
            onIndexChanged?.Invoke(CurrentIndex);
    }

    public void Prev()
    {
        if (options.Count == 0) return;
        int next = CurrentIndex - 1;
        if (next < 0) next = loop ? options.Count - 1 : 0;
        SetIndex(next);
    }

    public void Next()
    {
        if (options.Count == 0) return;
        int next = CurrentIndex + 1;
        if (next >= options.Count) next = loop ? 0 : options.Count - 1;
        SetIndex(next);
    }

    public void SetTitle(string text)
    {
        titleName = text;
        if (titleText != null) titleText.text = text;
    }

    private void UpdateButtons()
    {
        if (loop) return;
        if (prevButton != null) prevButton.interactable = CurrentIndex > 0;
        if (nextButton != null) nextButton.interactable = CurrentIndex < options.Count - 1;
    }

    private void AutoFind()
    {
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in texts)
        {
            string tName = t.gameObject.name.ToLower();
            if (titleText == null && (tName.Contains("title") || tName.Contains("name") || tName.Contains("label")))
                titleText = t;
            else if (valueText == null && (tName.Contains("value") || tName.Contains("option") || tName.Contains("display") || tName.Contains("text")))
                valueText = t;
        }

        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            string bName = b.gameObject.name.ToLower();
            if (prevButton == null && (bName.Contains("prev") || bName.Contains("left") || bName.Contains("minus") || bName.Contains("dec")))
                prevButton = b;
            else if (nextButton == null && (bName.Contains("next") || bName.Contains("right") || bName.Contains("plus") || bName.Contains("inc")))
                nextButton = b;
        }
    }

    private void BindEvents()
    {
        prevButton?.onClick.AddListener(Prev);
        nextButton?.onClick.AddListener(Next);
    }
}
