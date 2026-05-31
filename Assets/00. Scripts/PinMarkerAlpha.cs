using UnityEngine;

/// <summary>
/// 핀 마커(pinMarker)에 붙여 사용하는 컴포넌트.
///
/// 매 LateUpdate마다 씬의 모든 MapPiece를 순회하여
/// "자신보다 sortingOrder가 높으면서(= 앞에 있으면서) 핀 위치를 덮고 있는" 조각의 수를 센 뒤,
/// 그 수에 비례해 SpriteRenderer 알파를 낮춥니다.
///
/// ■ 시각적 크기 보정
///   MapPiece의 SpriteRenderer는 BoxCollider2D.size + mapPiecePadding 만큼 그려지므로
///   bounds 검사 시 동일한 패딩을 더합니다.
///
/// ■ 사용법
///   pinMarkerPrefab 루트(또는 그 SpriteRenderer가 있는 자식)에 이 컴포넌트를 추가하기만 하면 됩니다.
///   MapPiece.SetupPin()에서 Instantiate된 뒤 자동으로 동작합니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PinMarkerAlpha : MonoBehaviour
{
    [Tooltip("핀이 완전히 가려지지 않았을 때의 최대 알파 (0~1)")]
    [Range(0f, 1f)]
    public float maxAlpha = 1f;

    [Tooltip("조각 1개가 덮을 때마다 줄어드는 알파 양 (0~1)")]
    [Range(0f, 1f)]
    public float alphaPerCover = 0.35f;

    [Tooltip("최소 알파 (완전히 투명하게 하고 싶으면 0)")]
    [Range(0f, 1f)]
    public float minAlpha = 0.1f;

    [Tooltip("알파 보간 속도 (클수록 빠르게 변함)")]
    public float smoothSpeed = 8f;

    // ── 내부 ──────────────────────────────────────────────────────
    private SpriteRenderer sr;
    private MapPiece ownerPiece;       // 이 핀을 소유한 MapPiece
    private SpriteRenderer ownerSr;    // ownerPiece의 SpriteRenderer
    private float currentAlpha;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        currentAlpha = maxAlpha;
    }

    void Start()
    {
        // 부모 계층에서 MapPiece를 찾아 소유자로 등록
        ownerPiece = GetComponentInParent<MapPiece>();
        if (ownerPiece != null)
            ownerSr = ownerPiece.GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        int coverCount = CountCoveringPieces();
        float targetAlpha = Mathf.Max(minAlpha, maxAlpha - alphaPerCover * coverCount);

        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * smoothSpeed);

        Color c = sr.color;
        c.a = currentAlpha;
        sr.color = c;
    }

    /// <summary>
    /// 핀 위치를 덮고 있으면서 이 핀보다 앞에(높은 sortingOrder) 있는 MapPiece 수를 반환합니다.
    /// </summary>
    int CountCoveringPieces()
    {
        if (GameManager.Instance == null) return 0;

        MapPiece[] allPieces = GameManager.Instance.AllPieces;
        if (allPieces == null || allPieces.Length == 0) return 0;

        Vector2 pinWorldPos = transform.position;
        int ownerSortingOrder = ownerSr.sortingOrder;
        int count = 0;

        float padding = MapPieceManager.Instance != null
            ? MapPieceManager.Instance.mapPiecePadding
            : 0f;

        foreach (MapPiece piece in allPieces)
        {
            // 자신을 소유한 MapPiece(부모)는 건너뜀
            if (transform.IsChildOf(piece.transform)) continue;

            SpriteRenderer pieceSr = piece.GetComponent<SpriteRenderer>();
            if (pieceSr == null) continue;
            if (!pieceSr.enabled) continue;

            // 이 핀보다 앞에 있는 조각만 고려 (sortingOrder 높을수록 앞)
            if (pieceSr.sortingOrder <= ownerSortingOrder) continue;

            // 시각적 bounds = 콜라이더 bounds + padding
            if (IsPointInsideVisualBounds(piece, pinWorldPos, padding))
                count++;
        }

        return count;
    }

    /// <summary>
    /// MapPiece의 시각적 영역(BoxCollider2D bounds에 padding을 더한 영역) 안에
    /// 월드 좌표 point가 포함되는지 검사합니다.
    ///
    /// MapPiece 자체의 BoxCollider2D(pieceArea)를 사용합니다.
    /// 조각이 회전된 경우도 로컬 좌표 변환으로 정확히 처리합니다.
    /// </summary>
    bool IsPointInsideVisualBounds(MapPiece piece, Vector2 point, float padding)
    {
        BoxCollider2D col = piece.GetComponent<BoxCollider2D>();
        if (col == null) return false;

        // point를 MapPiece 로컬 좌표로 변환 (회전·스케일 포함)
        Vector2 localPoint = piece.transform.InverseTransformPoint(point);

        // 콜라이더 로컬 영역 (offset + size)
        Vector2 colOffset = col.offset;
        Vector2 colHalfSize = col.size * 0.5f;

        // padding은 월드 기준이므로 로컬로 환산
        // lossyScale의 역수를 곱해 로컬 패딩으로 변환
        Vector3 ls = piece.transform.lossyScale;
        float localPaddingX = Mathf.Abs(ls.x) > 0.0001f ? padding * 0.5f / Mathf.Abs(ls.x) : 0f;
        float localPaddingY = Mathf.Abs(ls.y) > 0.0001f ? padding * 0.5f / Mathf.Abs(ls.y) : 0f;

        float minX = colOffset.x - colHalfSize.x - localPaddingX;
        float maxX = colOffset.x + colHalfSize.x + localPaddingX;
        float minY = colOffset.y - colHalfSize.y - localPaddingY;
        float maxY = colOffset.y + colHalfSize.y + localPaddingY;

        return localPoint.x >= minX && localPoint.x <= maxX
            && localPoint.y >= minY && localPoint.y <= maxY;
    }
}