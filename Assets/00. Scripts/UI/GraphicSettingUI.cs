using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창(해상도/전체화면) UI 바인딩.
/// 드롭다운/토글 값은 "적용" 버튼을 눌러야 실제 화면에 반영된다.
/// "초기화" 버튼은 마지막으로 적용된 설정으로 UI만 되돌린다 (아직 적용 안 한 변경사항 취소).
/// </summary>
public class GraphicSettingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;

    private void OnEnable()
    {
        RefreshUIFromCurrentSettings();

        applyButton.onClick.AddListener(OnApplyClicked);
        resetButton.onClick.AddListener(OnResetClicked);
    }

    private void OnDisable()
    {
        applyButton.onClick.RemoveListener(OnApplyClicked);
        resetButton.onClick.RemoveListener(OnResetClicked);
    }

    // 창이 열릴 때 / 초기화 버튼을 눌렀을 때 : 현재 적용된 설정값으로 UI를 맞춘다
    private void RefreshUIFromCurrentSettings()
    {
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(DisplaySettings.GetResolutionLabels());
        resolutionDropdown.SetValueWithoutNotify(DisplaySettings.CurrentResolutionIndex);
        resolutionDropdown.RefreshShownValue();

        fullscreenToggle.SetIsOnWithoutNotify(DisplaySettings.IsFullscreen);
    }

    private void OnApplyClicked()
    {
        DisplaySettings.Apply(resolutionDropdown.value, fullscreenToggle.isOn);
    }

    // 초기화 버튼: 게임 기본 설정(시작 시점 해상도/전체화면)으로 UI를 되돌린다
    // Apply를 눌러야 실제 화면에 반영됨 (적용 버튼과 동일한 pending 방식 유지)
    private void OnResetClicked()
    {
        resolutionDropdown.SetValueWithoutNotify(DisplaySettings.GetDefaultResolutionIndex());
        resolutionDropdown.RefreshShownValue();

        fullscreenToggle.SetIsOnWithoutNotify(DisplaySettings.GetDefaultFullscreen());
    }
}