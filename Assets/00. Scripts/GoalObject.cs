using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Goal 오브젝트.
/// 플레이어가 닿으면 빨려들어가는 연출 후 다음 스테이지로 전환.
/// </summary>
public class GoalObject : MonoBehaviour
{
    [Header("Clear Effect")]
    [SerializeField] private float suckDuration = 1.2f;   // 빨려드는 시간
    [SerializeField] private float suckScale = 0.05f;  // 최종 크기 배율
    [SerializeField] private float waitAfter = 0.3f;   // 연출 후 씬 전환 대기

    [Header("Fade")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("Scene")]
    [SerializeField] private string selectSceneName = "StageSelectScene";

    private bool triggered = false;
    private PlayerController player;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (triggered || player == null) return;

        // Rigidbody 없으므로 직접 거리 체크
        if (Vector2.Distance(transform.position, player.transform.position) < 0.8f)
            StartCoroutine(ClearSequence());
    }

    IEnumerator ClearSequence()
    {
        triggered = true;

        // 플레이어 조작 불가
        player.SetControllable(false);

        // 드래그 중인 맵 조각 강제 중단 + 이후 드래그 불가
        MapPieceSelector.Instance?.StopAllDragging();

        // 플레이어 부모 해제
        MapPieceManager.Instance?.LockParent();
        player.transform.SetParent(null, worldPositionStays: true);
        //player.transform.localScale = Vector3.one;

        // 플레이어가 Goal로 빨려드는 연출
        Vector3 startPos = player.transform.position;
        Vector3 startScale = player.transform.localScale;
        Vector3 goalPos = transform.position;
        float elapsed = 0f;

        while (elapsed < suckDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / suckDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease in cubic

            player.transform.position = Vector3.Lerp(startPos, goalPos, eased);
            player.transform.localScale = Vector3.Lerp(startScale, startScale * suckScale, eased);
            yield return null;
        }

        yield return new WaitForSeconds(waitAfter);

        // 다음 스테이지 또는 선택 씬으로
        bool hasNext = ProgressManager.Instance != null
            ? ProgressManager.Instance.ClearCurrentStage()
            : false;

        string targetScene = hasNext
            ? SceneManager.GetActiveScene().name
            : selectSceneName;

        // 페이드 아웃 → 씬 전환 → 페이드 인
        FadeManager.Instance.FadeAndLoadScene(
            sceneName: targetScene,
            fadeOutDuration: fadeOutDuration,
            fadeInDuration: fadeInDuration
        );
    }
}