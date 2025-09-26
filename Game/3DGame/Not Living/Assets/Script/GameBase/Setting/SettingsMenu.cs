using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [Header("UIコンポーネント")]
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider mouseSensitivitySlider;
    
    [Header("値表示用テキスト")]
    public TextMeshProUGUI bgmValueText;
    public TextMeshProUGUI sfxValueText;
    public TextMeshProUGUI mouseSensitivityValueText;

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            bgmSlider.value = SettingsManager.Instance.BGMVolume;
            sfxSlider.value = SettingsManager.Instance.SFXVolume;
            // ▼▼▼ 参照するプロパティ名を変更 ▼▼▼
            mouseSensitivitySlider.value = SettingsManager.Instance.MouseSensitivityX;
            UpdateAllValueTexts();
        }
    }

    private void Start()
    {
        bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        // ▼▼▼ 呼び出すメソッド名を変更 ▼▼▼
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityXChanged);
    }

    private void OnBGMVolumeChanged(float value)
    {
        SettingsManager.Instance?.SetBGMVolume(value);
        UpdateBGMValueText(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        SettingsManager.Instance?.SetSFXVolume(value);
        UpdateSFXValueText(value);
    }

    // ▼▼▼ メソッド名を変更 ▼▼▼
    private void OnMouseSensitivityXChanged(float value)
    {
        SettingsManager.Instance?.SetMouseSensitivityX(value);
        UpdateMouseSensitivityValueText(value);
    }
    
    private void UpdateAllValueTexts()
    {
        UpdateBGMValueText(bgmSlider.value);
        UpdateSFXValueText(sfxSlider.value);
        UpdateMouseSensitivityValueText(mouseSensitivitySlider.value);
    }

    private void UpdateBGMValueText(float value)
    {
        if (bgmValueText != null) bgmValueText.text = (value * 100).ToString("F0");
    }

    private void UpdateSFXValueText(float value)
    {
        if (sfxValueText != null) sfxValueText.text = (value * 100).ToString("F0");
    }

    private void UpdateMouseSensitivityValueText(float value)
    {
        if (mouseSensitivityValueText != null) mouseSensitivityValueText.text = value.ToString("F2");
    }

    private void OnDestroy()
    {
        if(bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
        if(sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        // ▼▼▼ 解除するメソッド名を変更 ▼▼▼
        if(mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityXChanged);
    }
}