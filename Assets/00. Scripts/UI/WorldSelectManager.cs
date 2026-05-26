using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 스테이지 선택 씬 매니저
/// WorldListData ScriptableObject를 참조해 UI 구성
/// </summary>
public class WorldSelectManager : MonoBehaviour
{
    public static WorldSelectManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private WorldListData worldList;
    public WorldListData WorldList => worldList;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("World UI")]
    [SerializeField] private WorldCardUI mainCard;       // 중앙 메인 카드
    [SerializeField] private WorldCardUI prevPeekCard;   // 왼쪽 peek
    [SerializeField] private WorldCardUI nextPeekCard;   // 오른쪽 peek
    [SerializeField] private TextMeshProUGUI totalProgress;
    [SerializeField] private Button enterButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RectTransform dotsContainer;   // 하단 도트 인디케이터
    [SerializeField] private Color dotActiveColor = new Color(0.16f, 0.12f, 0.06f, 1f); // UIColors.Ink
    [SerializeField] private Color dotInactiveColor = new Color(0.63f, 0.54f, 0.37f, 0.5f); // UIColors.InkFaint
    [SerializeField] private float dotSize = 20f;
    [SerializeField] private float dotActiveWidth = 40f;  // 활성 도트 가로 길이
    [SerializeField] private float dotSpacing = 20f;

    [Header("Stage UI")]
    [SerializeField] private Transform stageButtonContainer;
    [SerializeField] private GameObject stageButtonPrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI worldLabel;
    [SerializeField] private TextMeshProUGUI worldName;
    [SerializeField] private TextMeshProUGUI worldDesc;
    [SerializeField] private TextMeshProUGUI worldProgress;
    [SerializeField] private Image worldIcon;
    [SerializeField] private Button recentButton;

    [Header("Transition")]
    [SerializeField] private RectTransform worldPanelRT;
    [SerializeField] private RectTransform stagePanelRT;
    [SerializeField] private float transitionDuration = 0.3f;
    
    private bool isTransitioning = false;

    private WorldData selectedWorld;

    private bool isWorldAnimating = false;
    private int currentWorldIndex = 0;

    private List<RectTransform> dotRects = new List<RectTransform>();
    private List<Image> dotImages = new List<Image>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(ShowWorldSelect);
        if (prevButton != null) prevButton.onClick.AddListener(PrevWorld);
        if (nextButton != null) nextButton.onClick.AddListener(NextWorld); 
        if (enterButton != null) enterButton.onClick.AddListener(OpenCurrentWorld);
        if (mainCard != null) mainCard.GetComponent<Button>().onClick.AddListener(OpenCurrentWorld);
        if (prevPeekCard != null) prevPeekCard.GetComponent<Button>().onClick.AddListener(PrevWorld);
        if (nextPeekCard != null) nextPeekCard.GetComponent<Button>().onClick.AddListener(NextWorld);

        BuildDots();
        RefreshCards();
    }

    void SelectWorld(WorldData world)
    {
        if (isTransitioning) return;
        selectedWorld = world;
        BuildStagePanel(world);
        StartCoroutine(TransitionToStage());
    }

    void ShowWorldSelect()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToWorld());
    }

    // ── 월드 버튼 ─────────────────────────────────────────────
    void OpenCurrentWorld()
    {
        var world = worldList.worlds[currentWorldIndex];
        if (GetUnlocked(world) == 0) return;
        SelectWorld(world);
    }

    void PrevWorld()
    {
        if (isWorldAnimating || currentWorldIndex <= 0) return;
        currentWorldIndex--;
        RefreshCards();
        StartCoroutine(AnimateTransition(Direction.Left));
    }

    void NextWorld()
    {
        if (isWorldAnimating || currentWorldIndex >= worldList.worlds.Count - 1) return;
        currentWorldIndex++;
        RefreshCards();
        StartCoroutine(AnimateTransition(Direction.Right));
    }

    void JumpToWorld(int idx)
    {
        if (isWorldAnimating || idx == currentWorldIndex) return;
        currentWorldIndex = idx;
        RefreshCards();
        StartCoroutine(AnimateTransition(idx > currentWorldIndex ? Direction.Right : Direction.Left));
    }

    enum Direction { Left, Right }

    IEnumerator AnimateTransition(Direction dir)
    {
        isWorldAnimating = true;

        float duration = 0.25f;
        float elapsed = 0f;

        // 애니메이션 시작 시점의 각 도트 width/color 스냅샷
        int count = dotRects.Count;
        float[] startWidths = new float[count];
        Color[] startColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            startWidths[i] = dotRects[i].sizeDelta.x;
            startColors[i] = dotImages[i].color;
        }

        // 목표 width/color
        float[] targetWidths = new float[count];
        Color[] targetColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            targetWidths[i] = i == currentWorldIndex ? dotActiveWidth : dotSize;
            targetColors[i] = i == currentWorldIndex ? dotActiveColor : dotInactiveColor;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease out cubic

            for (int i = 0; i < count; i++)
            {
                float w = Mathf.Lerp(startWidths[i], targetWidths[i], eased);
                dotRects[i].sizeDelta = new Vector2(w, dotSize);
                dotImages[i].color = Color.Lerp(startColors[i], targetColors[i], eased);
            }

            LayoutDots(currentWorldIndex); // 매 프레임 위치 재계산
            yield return null;
        }

        // 최종값 확정
        for (int i = 0; i < count; i++)
        {
            dotRects[i].sizeDelta = new Vector2(targetWidths[i], dotSize);
            dotImages[i].color = targetColors[i];
        }
        LayoutDots(currentWorldIndex);

        isWorldAnimating = false;
    }

    int GetUnlocked(WorldData world) => ProgressManager.Instance != null
    ? ProgressManager.Instance.GetUnlockedCount(world.worldId)
    : 0;

    void RefreshCards()
    {
        var worlds = worldList.worlds;

        // 메인 카드
        mainCard.Setup(worlds[currentWorldIndex]);
        mainCard.gameObject.SetActive(true);

        // 이전 peek
        bool hasPrev = currentWorldIndex > 0;
        prevPeekCard.gameObject.SetActive(hasPrev);
        if (hasPrev)
            prevPeekCard.Setup(worlds[currentWorldIndex - 1]);

        // 다음 peek
        bool hasNext = currentWorldIndex < worlds.Count - 1;
        nextPeekCard.gameObject.SetActive(hasNext);
        if (hasNext)
            nextPeekCard.Setup(worlds[currentWorldIndex + 1]);

        // 화살표 버튼 활성화
        if (prevButton) prevButton.interactable = hasPrev;
        if (nextButton) nextButton.interactable = hasNext;

        // 총 진행도 갱신
        RefreshTotalProgress();
    }

    void BuildDots()
    {
        if (dotsContainer == null) return;

        for (int i = dotRects.Count - 1; i >= 0; i--)
            if (dotRects[i] != null) Destroy(dotRects[i].gameObject);

        dotRects.Clear();
        dotImages.Clear();

        for (int i = 0; i < worldList.worlds.Count; i++)
        {
            var dot = new GameObject($"Dot_{i}");
            dot.transform.SetParent(dotsContainer, false);
            var img = dot.AddComponent<Image>();
            var rt = dot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            bool active = i == currentWorldIndex;
            img.color = active ? dotActiveColor : dotInactiveColor;
            rt.sizeDelta = new Vector2(active ? dotActiveWidth : dotSize, dotSize);

            dotRects.Add(rt);
            dotImages.Add(img);

            // 클릭으로 바로 이동
            int idx = i;
            var btn = dot.AddComponent<Button>();
            btn.onClick.AddListener(() => JumpToWorld(idx));
        }

        LayoutDots(currentWorldIndex, 1f); // 즉시 배치
    }

    void LayoutDots(int activeIdx, float lerp = 1f)
    {
        RectTransform prevButtonRT = prevButton.GetComponent<RectTransform>();
        RectTransform nextButtonRT = nextButton.GetComponent<RectTransform>();

        int count = dotRects.Count;
        if (count == 0) return;

        // 각 도트 너비 합산
        float dotsWidth = 0f;
        float[] widths = new float[count];
        for (int i = 0; i < count; i++)
        {
            widths[i] = dotRects[i].sizeDelta.x;
            dotsWidth += widths[i];
            if (i < count - 1) dotsWidth += dotSpacing;
        }

        // prevButton 오른쪽 끝을 기준점으로 사용 (prevButton 위치는 건드리지 않음)
        float prevRight = prevButtonRT.anchoredPosition.x + prevButtonRT.sizeDelta.x * 0.5f;

        // 도트들 위치: prevButton 오른쪽 끝 + spacing
        float dotStartX = prevRight + dotSpacing;
        float x = dotStartX;
        for (int i = 0; i < count; i++)
        {
            dotRects[i].anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0);
            x += widths[i] + dotSpacing;
        }

        // nextButton 위치: dots 오른쪽 끝 + spacing
        float nextW = nextButtonRT.sizeDelta.x;
        float nextStartX = dotStartX + dotsWidth + dotSpacing;
        nextButtonRT.anchoredPosition = new Vector2(
            nextStartX + nextW * 0.5f,
            nextButtonRT.anchoredPosition.y);
    }

    void RefreshTotalProgress()
    {
        if (totalProgress == null || worldList == null) return;

        int totalStages = 0;
        int totalCleared = 0;

        foreach (var world in worldList.worlds)
        {
            totalStages += world.stageFiles.Count;
            int unlocked = ProgressManager.Instance != null
                ? ProgressManager.Instance.GetUnlockedCount(world.worldId)
                : 1;
            totalCleared += Mathf.Max(0, unlocked - 1);
        }

        totalProgress.text = $"{totalCleared} / {totalStages}";
    }

    // ── 스테이지 버튼 ─────────────────────────────────────────
    void BuildStagePanel(WorldData world)
    {
        int total = world.stageFiles.Count;
        int unlockedCount = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetUnlockedCount(world.worldId)
            : 0;
        int cleared = Mathf.Max(0, unlockedCount - 1);

        if (worldLabel) worldLabel.text = $"WORLD {world.worldId.Split('_')[1].PadLeft(2, '0')}";
        if (worldName) worldName.text = world.displayName;
        if (worldDesc) worldDesc.text = world.displayDesc;
        if (worldProgress) worldProgress.text = $"{cleared} / {total}";
        if (worldIcon) worldIcon.sprite = world.displayIcon;

        // 스테이지 버튼 생성
        foreach (Transform child in stageButtonContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < world.stageFiles.Count; i++)
        {
            int idx = i;
            bool unlocked = i < unlockedCount;
            string name = GetStageName(world.stageFiles[i], i);

            GameObject btn = Instantiate(stageButtonPrefab, stageButtonContainer);

            float worldSeed = Mathf.Abs(world.worldId.GetHashCode()) % 1000 * 0.001f;
            float rotation = (Mathf.PerlinNoise(i * 0.7f, worldSeed) - 0.5f) * 6f; // -3 ~ +3도
            btn.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, rotation);

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = $"{i + 1}";

            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = unlocked;
                if (unlocked)
                    button.onClick.AddListener(() => StartStage(world, idx));
            }
        }

        if (recentButton)
        {
            int recentIndex = Mathf.Min(unlockedCount - 1, world.stageFiles.Count - 1);

            recentButton.onClick.RemoveAllListeners();
            recentButton.onClick.AddListener(() => StartStage(world, recentIndex));
            recentButton.GetComponentInChildren<TextMeshProUGUI>().text = $"스테이지 {recentIndex + 1} 시작";
        }
    }

    string GetStageName(string filePath, int fallback)
    {
        string path = Path.Combine(Application.streamingAssetsPath, filePath);
        try
        {
            var data = JsonUtility.FromJson<StageData>(File.ReadAllText(path));
            return string.IsNullOrEmpty(data.stageName)
                ? $"Stage {fallback + 1}"
                : data.stageName;
        }
        catch { return $"Stage {fallback + 1}"; }
    }

    void StartStage(WorldData world, int stageIndex)
    {
        ProgressManager.Instance?.StartWorld(world, stageIndex);
        SceneManager.LoadScene(gameSceneName);
    }

    // ── 트랜지션 ─────────────────────────────────────────
    IEnumerator TransitionToStage()
    {
        isTransitioning = true;
        float elapsed = 0f;
        float halfTime = transitionDuration * 0.5f;

        // World 패널 왼쪽으로 접히며 사라짐
        while (elapsed < halfTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfTime);
            float eased = t * t;

            worldPanelRT.localScale = new Vector3(1f - eased, 1f, 1f);
            worldPanelRT.anchoredPosition = new Vector2(
                Mathf.Lerp(0, -worldPanelRT.rect.width * 0.5f, eased), 0);

            yield return null;
        }

        worldPanelRT.gameObject.SetActive(false);
        worldPanelRT.localScale = Vector3.one;
        worldPanelRT.anchoredPosition = Vector2.zero;
        isTransitioning = false;
    }

    IEnumerator TransitionToWorld()
    {
        isTransitioning = true;
        float elapsed = 0f;
        float halfTime = transitionDuration * 0.5f;

        worldPanelRT.gameObject.SetActive(true);
        worldPanelRT.localScale = new Vector3(0f, 1f, 1f);
        worldPanelRT.anchoredPosition = new Vector2(-worldPanelRT.rect.width * 0.5f, 0);

        // World 패널 왼쪽에서 펼쳐지며 나타남
        while (elapsed < halfTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfTime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            worldPanelRT.localScale = new Vector3(eased, 1f, 1f);
            worldPanelRT.anchoredPosition = new Vector2(
                Mathf.Lerp(-worldPanelRT.rect.width * 0.5f, 0, eased), 0);

            yield return null;
        }

        worldPanelRT.localScale = Vector3.one;
        worldPanelRT.anchoredPosition = Vector2.zero;
        selectedWorld = null;
        isTransitioning = false;
    }
}