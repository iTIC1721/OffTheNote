using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI worldLabel;
    [SerializeField] private TextMeshProUGUI worldName;
    [SerializeField] private TextMeshProUGUI worldDesc;
    [SerializeField] private TextMeshProUGUI worldShortDesc;
    [SerializeField] private TextMeshProUGUI worldProgress;
    [SerializeField] private TextMeshProUGUI worldProgressBar;
    [SerializeField] private Image worldIcon;
    [SerializeField] private GameObject lockOverlay;

    private Coroutine _unlockCoroutine;

    public void Setup(WorldData world, bool forceShowLock = false)
    {
        // 진행 중인 해금 연출 중단 후 lockOverlay 상태 초기화
        if (_unlockCoroutine != null)
        {
            StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = null;
            if (lockOverlay != null)
            {
                var cg = lockOverlay.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                var contents = lockOverlay.transform.Find("Contents");
                if (contents != null) contents.localScale = Vector3.one;
            }
        }

        int total = world.stageFiles.Count;
        int unlocked = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetUnlockedCount(world.worldId)
            : 0;
        int cleared = Mathf.Max(0, unlocked - 1);

        bool playable = unlocked > 0;

        if (worldLabel) worldLabel.text = $"WORLD {world.worldId.Split('_')[1].PadLeft(2, '0')}";
        if (worldName) worldName.text = world.displayName;
        if (worldDesc) worldDesc.text = world.displayDesc;
        if (worldShortDesc) worldShortDesc.text = world.displayShortDesc;
        if (worldProgress) worldProgress.text = $"{cleared} / {total}";
        if (worldProgressBar)
        {
            string progress = "";
            int i = 0;
            for (; i < cleared; i++) progress += "■";
            for (; i < total; i++) progress += "□";
            worldProgressBar.text = progress;
        }
        if (worldIcon) worldIcon.sprite = world.displayIcon;
        if (lockOverlay) lockOverlay.SetActive(!playable || forceShowLock);
    }

    /// <summary>
    /// 잠금 해제 연출. lockOverlay를 스케일 팝 + 페이드 아웃으로 제거합니다.
    /// </summary>
    public void PlayUnlockEffect(float delay = 0.3f)
    {
        if (lockOverlay == null || !lockOverlay.activeSelf) return;
        if (_unlockCoroutine != null) StopCoroutine(_unlockCoroutine);
        _unlockCoroutine = StartCoroutine(UnlockCoroutine(delay));
    }

    IEnumerator UnlockCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // lockOverlay 전체에 페이드 (Locked 배경 포함)
        var cg = lockOverlay.GetComponent<CanvasGroup>();
        if (cg == null) cg = lockOverlay.AddComponent<CanvasGroup>();

        // Icon, Title을 묶은 Contents 오브젝트에만 스케일 팝
        Transform contents = lockOverlay.transform.Find("Contents");
        Vector3 baseScale = contents != null ? contents.localScale : Vector3.one;

        float duration = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease out cubic

            // 전체 페이드 아웃
            cg.alpha = 1f - eased;

            // Contents만 스케일 팝
            if (contents != null)
            {
                float scalePop = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                contents.localScale = baseScale * scalePop;
            }

            yield return null;
        }

        if (contents != null)
            contents.localScale = baseScale;

        cg.alpha = 1f;
        lockOverlay.SetActive(false);
    }
}