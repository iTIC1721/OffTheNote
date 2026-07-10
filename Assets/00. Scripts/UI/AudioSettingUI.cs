using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 설정 창(마스터/BGM/SFX 볼륨) UI 바인딩.
/// SettingsUI(해상도/전체화면)와 동일한 방식: 슬라이더 값은 "적용" 버튼을 눌러야
/// 실제 AudioManager에 반영되고, "초기화" 버튼은 기본 볼륨값으로 UI만 되돌린다.
/// </summary>
public class AudioSettingUI : MonoBehaviour
{
    [Header("Master")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeLabel;

    [Header("BGM")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TextMeshProUGUI bgmVolumeLabel;

    [Header("SFX")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sfxVolumeLabel;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;

    private void OnEnable()
    {
        RefreshUIFromCurrentSettings();

        masterVolumeSlider.onValueChanged.AddListener(OnMasterSliderChanged);
        bgmVolumeSlider.onValueChanged.AddListener(OnBGMSliderChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXSliderChanged);

        applyButton.onClick.AddListener(OnApplyClicked);
        resetButton.onClick.AddListener(OnResetClicked);
    }

    private void OnDisable()
    {
        masterVolumeSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
        bgmVolumeSlider.onValueChanged.RemoveListener(OnBGMSliderChanged);
        sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXSliderChanged);

        applyButton.onClick.RemoveListener(OnApplyClicked);
        resetButton.onClick.RemoveListener(OnResetClicked);
    }

    // 창이 열릴 때: 현재 AudioManager에 실제 적용되어 있는 값으로 UI를 맞춘다
    private void RefreshUIFromCurrentSettings()
    {
        SetSliderWithoutNotify(masterVolumeSlider, masterVolumeLabel, AudioManager.Instance.MasterVolume);
        SetSliderWithoutNotify(bgmVolumeSlider, bgmVolumeLabel, AudioManager.Instance.BGMVolume);
        SetSliderWithoutNotify(sfxVolumeSlider, sfxVolumeLabel, AudioManager.Instance.SFXVolume);
    }

    // 슬라이더를 움직이는 동안에는 라벨만 실시간으로 갱신 (아직 AudioManager에는 반영 안 함)
    private void OnMasterSliderChanged(float value) => UpdateLabel(masterVolumeLabel, value);
    private void OnBGMSliderChanged(float value) => UpdateLabel(bgmVolumeLabel, value);
    private void OnSFXSliderChanged(float value) => UpdateLabel(sfxVolumeLabel, value);

    // 슬라이더는 0~1(Whole Numbers 해제) 기준, 라벨에는 0~100 정수로 표시
    private void UpdateLabel(TextMeshProUGUI label, float linearVolume)
    {
        label.text = Mathf.RoundToInt(linearVolume * 100f).ToString();
    }

    private void OnApplyClicked()
    {
        // AudioManager 프로퍼티 setter가 Mixer 반영 + PlayerPrefs 저장까지 처리
        AudioManager.Instance.MasterVolume = masterVolumeSlider.value;
        AudioManager.Instance.BGMVolume = bgmVolumeSlider.value;
        AudioManager.Instance.SFXVolume = sfxVolumeSlider.value;
    }

    // 초기화 버튼: 기본 볼륨값으로 UI만 되돌린다. Apply를 눌러야 실제 반영됨
    private void OnResetClicked()
    {
        SetSliderWithoutNotify(masterVolumeSlider, masterVolumeLabel, AudioManager.Instance.DefaultMasterVolume);
        SetSliderWithoutNotify(bgmVolumeSlider, bgmVolumeLabel, AudioManager.Instance.DefaultBGMVolume);
        SetSliderWithoutNotify(sfxVolumeSlider, sfxVolumeLabel, AudioManager.Instance.DefaultSFXVolume);
    }

    private void SetSliderWithoutNotify(Slider slider, TextMeshProUGUI label, float linearVolume)
    {
        slider.SetValueWithoutNotify(linearVolume);
        UpdateLabel(label, linearVolume);
    }
}