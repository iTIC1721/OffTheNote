using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapPieceSelector : MonoBehaviour
{
    public static MapPieceSelector Instance { get; private set; }

    [SerializeField] private float respawnFreezeDuration = 1.5f;
    private Coroutine freezeCoroutine;

    public MapPiece DraggingPiece => draggingPiece;
    private MapPiece draggingPiece;

    private Camera mainCam;
    private List<MapPiece> recentMoveOrder = new List<MapPiece>();

    private bool isDragEnabled = true;

    void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
    }

    void Update()
    {
        if (!isDragEnabled) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);

            List<MapPiece> candidates = new List<MapPiece>();
            foreach (var hit in hits)
            {
                MapPiece piece = hit.GetComponent<MapPiece>();
                if (piece != null && (piece.IsMovable || piece.IsPinned || piece.IsFlippable) && !candidates.Contains(piece))
                    candidates.Add(piece);
            }

            if (candidates.Count == 0) return;

            draggingPiece = GetTopPriority(candidates);
            draggingPiece.StartDrag();

            recentMoveOrder.Remove(draggingPiece);
            recentMoveOrder.Add(draggingPiece);
        }

        if (Input.GetMouseButtonUp(0))
        {
            draggingPiece?.StopDrag();
            draggingPiece = null;
        }
    }

    MapPiece GetTopPriority(List<MapPiece> candidates)
    {
        // 1순위: 플레이어가 속한 조각들로 범위 축소
        List<MapPiece> playerPieces = GetPlayerPieces();
        List<MapPiece> playerCandidates = candidates.FindAll(p => playerPieces.Contains(p));

        List<MapPiece> pool = playerCandidates.Count > 0 ? playerCandidates : candidates;

        // 2순위: pool 안에서 가장 최근에 움직인 조각
        for (int i = recentMoveOrder.Count - 1; i >= 0; i--)
        {
            if (pool.Contains(recentMoveOrder[i]))
                return recentMoveOrder[i];
        }

        // 3순위: pool 안에서 sortingOrder가 가장 높은 조각
        MapPiece topRendered = null;
        int topOrder = int.MinValue;
        foreach (var piece in pool)
        {
            SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            if (order > topOrder)
            {
                topOrder = order;
                topRendered = piece;
            }
        }
        return topRendered;
    }

    List<MapPiece> GetPlayerPieces()
    {
        List<MapPiece> result = new List<MapPiece>();
        if (GameManager.Instance?.AllPieces == null) return result;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null) return result;

        Vector2 playerPos = player.transform.position;
        foreach (var piece in GameManager.Instance.AllPieces)
        {
            if (piece.ContainsPlayer(playerPos))
                result.Add(piece);
        }
        return result;
    }

    public void StopAllDragging()
    {
        isDragEnabled = false;
        draggingPiece?.StopDrag();
        draggingPiece = null;
    }

    public void EnableDragging(bool enable)
    {
        isDragEnabled = enable;
    }

    public void StartRespawnFreeze()
    {
        if (freezeCoroutine != null)
            StopCoroutine(freezeCoroutine);
        freezeCoroutine = StartCoroutine(RespawnFreeze());
    }

    IEnumerator RespawnFreeze()
    {
        isDragEnabled = false;
        draggingPiece?.StopDrag();
        draggingPiece = null;

        yield return new WaitForSecondsRealtime(respawnFreezeDuration);

        // 일시정지 중이 아닐 때만 복구
        if (PauseManager.Instance == null || !PauseManager.Instance.IsPaused)
            isDragEnabled = true;

        freezeCoroutine = null;
    }
}