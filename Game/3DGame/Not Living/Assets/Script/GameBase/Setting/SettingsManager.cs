using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("オーディオ設定")]
    [SerializeField] private AudioMixer gameAudioMixer;
    [SerializeField] private string bgmVolumeParam = "BGM_Volume";
    [SerializeField] private string sfxVolumeParam = "SFX_Volume";

    // ▼▼▼ プロパティ名を変更 ▼▼▼
    public float BGMVolume { get; private set; }
    public float SFXVolume { get; private set; }
    public float MouseSensitivityX { get; private set; } // MouseSensitivity -> MouseSensitivityX

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadSettings()
    {
        BGMVolume = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        // ▼▼▼ 読み込むキーと変数名を変更 ▼▼▼
        MouseSensitivityX = PlayerPrefs.GetFloat("MouseSensitivityX", 1.0f);

        ApplyBGMVolume();
        ApplySFXVolume();
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
        ApplyBGMVolume();
        PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        ApplySFXVolume();
        PlayerPrefs.SetFloat("SFXVolume", SFXVolume);
    }

    // ▼▼▼ メソッド名と変数名を変更 ▼▼▼
    public void SetMouseSensitivityX(float sensitivity)
    {
        MouseSensitivityX = sensitivity;
        PlayerPrefs.SetFloat("MouseSensitivityX", MouseSensitivityX);
    }

    private void ApplyBGMVolume()
    {
        gameAudioMixer.SetFloat(bgmVolumeParam, Mathf.Log10(BGMVolume) * 20);
    }

    private void ApplySFXVolume()
    {
        gameAudioMixer.SetFloat(sfxVolumeParam, Mathf.Log10(SFXVolume) * 20);
    }
}