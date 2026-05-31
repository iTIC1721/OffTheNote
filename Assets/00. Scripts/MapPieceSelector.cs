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
        }

        if (Input.GetMouseButtonUp(0))
        {
            draggingPiece?.StopDrag();
            draggingPiece = null;
        }
    }

    MapPiece GetTopPriority(List<MapPiece> candidates)
    {
        MapPiece top = null;
        int topOrder = int.MinValue;
        foreach (var piece in candidates)
        {
            SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            if (order > topOrder)
            {
                topOrder = order;
                top = piece;
            }
        }
        return top;
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