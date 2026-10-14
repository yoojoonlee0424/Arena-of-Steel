using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingSliderControl : MonoBehaviour
{
    [Header("UI 바인딩")]
    [SerializeField] protected TextMeshProUGUI titleText;
    [SerializeField] protected Slider slider;
    [SerializeField] protected TMP_InputField inputField;
    [SerializeField] protected TextMeshProUGUI valueText;
    [SerializeField] protected Button decreaseButton;
    [SerializeField] protected Button increaseButton;

    [Header("값 범위 및 단위")]
    [SerializeField] protected string titleName = "";
    [SerializeField] protected float minValue = 0f;
    [SerializeField] protected float maxValue = 1f;
    [SerializeField] protected float step = 0.05f;
    [SerializeField] protected bool displayAsPercent = false;
    [SerializeField] protected string valueFormat = "F1";

    public float Value { get; protected set; }
    public event Action<float> onValueChanged;

    protected bool isInternalUpdating;

    protected virtual void Awake()
    {
        AutoFindComponents();
        BindEvents();
    }

    protected virtual void Reset()
    {
        AutoFindComponents();
    }

    public virtual void Init(float initialValue)
    {
        SetValue(initialValue, notify: false);
    }

    public virtual void SetValue(float val, bool notify = true)
    {
        Value = Mathf.Clamp(val, minValue, maxValue);

        isInternalUpdating = true;

        if (slider != null)
        {
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.SetValueWithoutNotify(Value);
        }

        UpdateDisplay();
        isInternalUpdating = false;

        if (notify)
        {
            onValueChanged?.Invoke(Value);
        }
    }

    public virtual void AutoFindComponents()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (inputField == null) inputField = GetComponentInChildren<TMP_InputField>(true);

        if (valueText == null && inputField == null)
        {
            valueText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (decreaseButton == null || increaseButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string bName = btn.gameObject.name.ToLower();
                if (decreaseButton == null && (bName.Contains("dec") || bName.Contains("minus") || bName.Contains("prev") || bName.Contains("left")))
                {
                    decreaseButton = btn;
                }
                else if (increaseButton == null && (bName.Contains("inc") || bName.Contains("plus") || bName.Contains("next") || bName.Contains("right")))
                {
                    increaseButton = btn;
                }
            }
        }
    }

    protected virtual void BindEvents()
    {
        if (slider != null)
        {
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        if (decreaseButton != null)
        {
            decreaseButton.onClick.AddListener(OnDecreaseClicked);
        }

        if (increaseButton != null)
        {
            increaseButton.onClick.AddListener(OnIncreaseClicked);
        }
    }

    protected virtual void OnSliderChanged(float val)
    {
        if (isInternalUpdating) return;
        SetValue(val, notify: true);
    }

    protected virtual void OnInputEndEdit(string text)
    {
        if (isInternalUpdating) return;

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            float targetValue = displayAsPercent ? parsed * 0.01f : parsed;
            SetValue(targetValue, notify: true);
        }
        else
        {
            UpdateDisplay();
        }
    }

    protected virtual void OnDecreaseClicked()
    {
        AudioManager.Instance?.PlayNavigate();
        SetValue(Value - step, notify: true);
    }

    protected virtual void OnIncreaseClicked()
    {
        AudioManager.Instance?.PlayNavigate();
        SetValue(Value + step, notify: true);
    }

    protected virtual void UpdateDisplay()
    {
        float displayVal = displayAsPercent ? Value * 100f : Value;
        string formatted = displayVal.ToString(valueFormat, CultureInfo.InvariantCulture);

        if (inputField != null)
        {
            inputField.SetTextWithoutNotify(formatted);
        }

        if (valueText != null)
        {
            valueText.text = formatted;
        }

        if (titleText != null)
        {
            titleText.text = titleName;
        }
    }

    public virtual void SetInteractable(bool interactable)
    {
        if (slider != null) slider.interactable = interactable;
        if (inputField != null) inputField.interactable = interactable;
        if (decreaseButton != null) decreaseButton.interactable = interactable;
        if (increaseButton != null) increaseButton.interactable = interactable;
    }
}
