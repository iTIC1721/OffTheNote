using UnityEngine;

/// <summary>
/// 위험 구역 오브젝트.
/// 플레이어가 닿으면 즉시 리스폰.
/// </summary>
public class HazardObject : MonoBehaviour
{
    [SerializeField] private float margin = 0.1f;

    private PlayerController player;
    private BoxCollider2D playerCol;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        playerCol = player?.GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (player == null || playerCol == null) return;

        // lossyScale 반영한 실제 월드 크기에서 margin만큼 축소
        Vector3 scale = player.transform.lossyScale;
        Vector2 worldSize = new Vector2(
            playerCol.size.x * Mathf.Abs(scale.x) - margin * 2f,
            playerCol.size.y * Mathf.Abs(scale.y) - margin * 2f);
        worldSize = new Vector2(Mathf.Max(0.01f, worldSize.x), Mathf.Max(0.01f, worldSize.y));

        // 플레이어 캡슐 위치에서 이 오브젝트의 콜라이더와 겹치는지 체크
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            player.transform.position,
            worldSize,
            0f
        );

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject)
            {
                player.Respawn();
                return;
            }
        }
    }
}