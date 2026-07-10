using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 해상도 및 전체화면 여부를 관리하는 정적 클래스.
/// 설정 화면(SettingsUI)에서만 호출되므로 MonoBehaviour/싱글톤 불필요.
/// 실제 값 저장은 PlayerPrefs가 담당하므로 씬 전환/재시작과도 무관하게 유지된다.
/// </summary>
public static class DisplaySettings
{
    private const string PREF_RES_WIDTH = "Settings_ResolutionWidth";
    private const string PREF_RES_HEIGHT = "Settings_ResolutionHeight";
    private const string PREF_FULLSCREEN = "Settings_Fullscreen";

    private static List<Resolution> availableResolutions;
    private static bool isInitialized = false;

    // 게임 시작 시점의 해상도/전체화면 상태를 "기본값"으로 간주 (Project Settings 기준)
    private static Resolution defaultResolution;
    private static bool defaultFullscreen;

    public static int CurrentResolutionIndex { get; private set; }
    public static bool IsFullscreen { get; private set; }

    // 최초 접근 시점에만 초기화 (씬 로드 순서에 의존하지 않음)
    // 주의: 이 시점 이전에 이미 Screen.SetResolution()이 호출된 적이 있다면
    // 기본값이 아니라 그 값을 "기본값"으로 잘못 캡처하게 되니 주의할 것
    private static void EnsureInitialized()
    {
        if (isInitialized) return;

        BuildResolutionList();

        defaultResolution = Screen.currentResolution;
        defaultFullscreen = Screen.fullScreen;

        LoadSettings();
        isInitialized = true;
    }

    private static void BuildResolutionList()
    {
        availableResolutions = Screen.resolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(g => g.First())
            .OrderBy(r => r.width)
            .ThenBy(r => r.height)
            .ToList();

        if (availableResolutions.Count == 0)
        {
            availableResolutions.Add(Screen.currentResolution);
        }
    }

    /// <summary>UI 드롭다운에 넣을 "1920 x 1080" 형식 라벨 목록</summary>
    public static List<string> GetResolutionLabels()
    {
        EnsureInitialized();
        return availableResolutions.Select(r => $"{r.width} x {r.height}").ToList();
    }

    /// <summary>기본(게임 시작 시점) 해상도에 해당하는 드롭다운 인덱스</summary>
    public static int GetDefaultResolutionIndex()
    {
        EnsureInitialized();

        int foundIndex = availableResolutions.FindIndex(
            r => r.width == defaultResolution.width && r.height == defaultResolution.height);
        return foundIndex >= 0 ? foundIndex : availableResolutions.Count - 1;
    }

    /// <summary>기본(게임 시작 시점) 전체화면 여부</summary>
    public static bool GetDefaultFullscreen()
    {
        EnsureInitialized();
        return defaultFullscreen;
    }

    /// <summary>드롭다운 인덱스 + 전체화면 여부를 실제 화면에 적용하고 저장</summary>
    public static void Apply(int resolutionIndex, bool fullscreen)
    {
        EnsureInitialized();

        if (resolutionIndex < 0 || resolutionIndex >= availableResolutions.Count)
        {
            resolutionIndex = CurrentResolutionIndex;
        }

        CurrentResolutionIndex = resolutionIndex;
        IsFullscreen = fullscreen;

        Resolution res = availableResolutions[CurrentResolutionIndex];
        Screen.SetResolution(res.width, res.height, IsFullscreen);

        SaveSettings();
    }

    private static void SaveSettings()
    {
        Resolution res = availableResolutions[CurrentResolutionIndex];
        PlayerPrefs.SetInt(PREF_RES_WIDTH, res.width);
        PlayerPrefs.SetInt(PREF_RES_HEIGHT, res.height);
        PlayerPrefs.SetInt(PREF_FULLSCREEN, IsFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void LoadSettings()
    {
        int savedWidth = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.currentResolution.height);
        IsFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;

        int foundIndex = availableResolutions.FindIndex(r => r.width == savedWidth && r.height == savedHeight);
        CurrentResolutionIndex = foundIndex >= 0 ? foundIndex : availableResolutions.Count - 1;
    }
}