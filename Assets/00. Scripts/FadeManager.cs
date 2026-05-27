using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 어디서나 사용할 수 있는 범용 페이드 인/아웃 매니저.
/// DontDestroyOnLoad 싱글톤으로 씬 전환에도 유지됩니다.
///
/// ── 사용법 ──────────────────────────────────────────
/// // 단순 페이드 아웃
/// FadeManager.Instance.FadeOut(duration: 0.5f);
///
/// // 페이드 아웃 → 씬 로드 대기 → 페이드 인  ★씬 전환 권장 패턴
/// FadeManager.Instance.FadeAndLoadScene("GameScene",
///     fadeOutDuration: 0.4f,
///     fadeInDuration:  0.5f
/// );
///
/// // 페이드 아웃 → 콜백 → 페이드 인  (씬 전환 외 용도)
/// FadeManager.Instance.FadeOutIn(
///     fadeOutDuration: 0.3f,
///     fadeInDuration:  0.3f,
///     onMidpoint: () => { /* 리스폰 처리 등 */ }
/// );
///
/// // 즉시 검정으로 덮은 뒤 페이드 인
/// FadeManager.Instance.FadeIn(duration: 0.6f);
/// ────────────────────────────────────────────────────
/// </summary>
public class FadeManager : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────
    public static FadeManager Instance { get; private set; }

    // ── Inspector ────────────────────────────────────
    [Header("Fade Settings")]
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float defaultDuration = 0.4f;

    [Header("Sort Order")]
    [Tooltip("다른 Canvas UI보다 위에 그려지도록 높은 값을 사용하세요.")]
    [SerializeField] private int sortingOrder = 999;

    // ── Private ──────────────────────────────────────
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Coroutine _currentFade;

    // ── 상태 프로퍼티 ─────────────────────────────────
    /// <summary>현재 페이드가 진행 중이면 true</summary>
    public bool IsFading => _currentFade != null;

    /// <summary>화면이 완전히 가려진 상태(alpha == 1)이면 true</summary>
    public bool IsOpaque => Mathf.Approximately(_canvasGroup.alpha, 1f);

    // ─────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeUI();
    }

    #endregion

    // ─────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// 현재 화면 → 검정. 화면을 가립니다.
    /// </summary>
    public void FadeOut(float? duration = null, Action onComplete = null)
    {
        StartFade(0f, 1f, duration ?? defaultDuration, onComplete);
    }

    /// <summary>
    /// 검정 → 현재 화면. 화면을 드러냅니다.
    /// </summary>
    public void FadeIn(float? duration = null, Action onComplete = null)
    {
        StartFade(1f, 0f, duration ?? defaultDuration, onComplete);
    }

    /// <summary>
    /// 씬 전환 전용 페이드.
    /// 페이드 아웃 완료 → LoadSceneAsync 시작 → 로드 완전히 끝난 뒤 페이드 인.
    /// 씬 로드 스파이크가 검정 화면 뒤에 숨겨지므로 끊김이 없습니다.
    /// </summary>
    /// <param name="sceneName">로드할 씬 이름.</param>
    /// <param name="fadeOutDuration">아웃 시간 (초).</param>
    /// <param name="fadeInDuration">인 시간 (초).</param>
    /// <param name="onBeforeLoad">씬 로드 직전 콜백 (상태 정리 등).</param>
    /// <param name="onComplete">페이드 인 완료 후 콜백.</param>
    public void FadeAndLoadScene(
        string sceneName,
        float fadeOutDuration = 0f,
        float fadeInDuration = 0f,
        Action onBeforeLoad = null,
        Action onComplete = null)
    {
        float outDur = fadeOutDuration > 0f ? fadeOutDuration : defaultDuration;
        float inDur = fadeInDuration > 0f ? fadeInDuration : defaultDuration;

        StopCurrentFade();
        _currentFade = StartCoroutine(
            FadeAndLoadCoroutine(sceneName, outDur, inDur, onBeforeLoad, onComplete)
        );
    }

    /// <summary>
    /// 페이드 아웃 → onMidpoint 콜백 → 페이드 인.
    /// 씬 전환 이외의 용도(리스폰, 연출 등)에 사용하세요.
    /// 씬 전환은 FadeAndLoadScene을 권장합니다.
    /// </summary>
    public void FadeOutIn(
        float fadeOutDuration = 0f,
        float fadeInDuration = 0f,
        Action onMidpoint = null,
        Action onComplete = null,
        float holdDuration = 0f)
    {
        float outDur = fadeOutDuration > 0f ? fadeOutDuration : defaultDuration;
        float inDur = fadeInDuration > 0f ? fadeInDuration : defaultDuration;

        StopCurrentFade();
        _currentFade = StartCoroutine(
            FadeOutInCoroutine(outDur, inDur, holdDuration, onMidpoint, onComplete)
        );
    }

    /// <summary>
    /// 진행 중인 페이드를 즉시 중단합니다.
    /// </summary>
    /// <param name="setOpaque">true 면 검정으로, false 면 투명으로 고정합니다.</param>
    public void StopFade(bool setOpaque = false)
    {
        StopCurrentFade();
        _canvasGroup.alpha = setOpaque ? 1f : 0f;
        _canvasGroup.blocksRaycasts = setOpaque;
    }

    /// <summary>
    /// 페이드 색상을 런타임에 변경합니다.
    /// </summary>
    public void SetFadeColor(Color color)
    {
        fadeColor = color;
        var img = GetComponentInChildren<Image>();
        if (img != null) img.color = color;
    }

    #endregion

    // ─────────────────────────────────────────────────
    #region Internal

    private void BuildFadeUI()
    {
        // Canvas
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;

        // CanvasScaler
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // GraphicRaycaster — 입력 차단용
        gameObject.AddComponent<GraphicRaycaster>();

        // 전체 화면 덮는 이미지
        var imgObj = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(transform, false);

        var rect = imgObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = imgObj.GetComponent<Image>();
        img.color = fadeColor;
        img.raycastTarget = true;

        // CanvasGroup — alpha 조절 + 입력 차단
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void StartFade(float from, float to, float duration, Action onComplete)
    {
        StopCurrentFade();
        _currentFade = StartCoroutine(FadeCoroutine(from, to, duration, onComplete));
    }

    private void StopCurrentFade()
    {
        if (_currentFade != null)
        {
            StopCoroutine(_currentFade);
            _currentFade = null;
        }
    }

    private IEnumerator FadeCoroutine(float from, float to, float duration, Action onComplete)
    {
        _canvasGroup.alpha = from;
        _canvasGroup.blocksRaycasts = (from > 0f || to > 0f);

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
        }

        _canvasGroup.alpha = to;
        _canvasGroup.blocksRaycasts = (to > 0f);

        onComplete?.Invoke();
    }

    /// <summary>
    /// 씬 전환 전용 코루틴.
    /// 페이드 아웃 → LoadSceneAsync(allowSceneActivation=false로 대기)
    /// → 로드 완료(progress >= 0.9f) 확인 → 활성화 → 페이드 인
    /// </summary>
    private IEnumerator FadeAndLoadCoroutine(
        string sceneName,
        float outDur,
        float inDur,
        Action onBeforeLoad,
        Action onComplete)
    {
        // 1. 페이드 아웃
        yield return FadeCoroutine(0f, 1f, outDur, null);

        // 2. 씬 로드 시작 (활성화는 아직 보류)
        onBeforeLoad?.Invoke();
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // 3. 로드가 90%까지 완료될 때까지 대기 (Unity 씬 로드 특성상 90%가 준비 완료)
        while (op.progress < 0.9f)
            yield return null;

        // 4. 씬 활성화 (실제 씬 전환 — 이 시점에 프리즈 없음)
        op.allowSceneActivation = true;

        // 5. 씬 전환 완료 대기 (isDone == true)
        while (!op.isDone)
            yield return null;

        // 6. 새 씬 첫 프레임 렌더링 후 페이드 인
        yield return null;
        yield return FadeCoroutine(1f, 0f, inDur, null);

        _currentFade = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeOutInCoroutine(
        float outDur,
        float inDur,
        float holdDur,
        Action onMidpoint,
        Action onComplete)
    {
        yield return FadeCoroutine(0f, 1f, outDur, null);

        onMidpoint?.Invoke();
        if (holdDur > 0f)
            yield return new WaitForSecondsRealtime(holdDur);

        yield return FadeCoroutine(1f, 0f, inDur, null);

        _currentFade = null;
        onComplete?.Invoke();
    }

    #endregion
}