using UnityEngine;

public class MapPieceManager : MonoBehaviour
{
    public static MapPieceManager Instance { get; private set; }

    [SerializeField] private PlayerController player;

    [Header("Drag Settings")]
    public int sortingOrderWhenDragged = 10;
    public float dragSmoothSpeed = 10f;
    public float maxSpeed = 30f; // 유니티 유닛/초

    [Header("Settings")]
    public float mapPiecePadding = 0.3f;

    [Header("BG")]
    public Color color;
    public Sprite movableSprite;
    public Sprite immovableSprite;

    public MapPiece CurrentPiece => currentPiece;

    private MapPiece currentPiece;
    private Transform sceneRoot; // 어느 조각에도 없을 때 부모
    private Vector2 lastPiecePosition;

    private MapPiece subscribedPiece;

    private bool isParentLocked = false;

    // 플립 진행 중 플래그 - DetectAndReparent/ResolveExternalOverlap 억제용
    private bool isFlipping = false;
    public void SetFlipping(bool flipping) => isFlipping = flipping;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        sceneRoot = player.transform.parent;
    }

    void FixedUpdate()
    {
        DetectAndReparent();
        TrackPieceMovement();
    }

    public void LockParent()
    {
        isParentLocked = true;
    }

    public void DetachPlayerFromPiece(MapPiece piece)
    {
        if (currentPiece != piece) return;

        // 플레이어를 씬 루트로 분리
        currentPiece = null;
        player.transform.SetParent(sceneRoot, worldPositionStays: true);
        player.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 플레이어가 지정한 MapPiece 위에 있으면(자식 계층에 속하면) 플레이어를 반환.
    /// 플립 시작 시 조작 정지 대상을 결정하는 데 사용.
    /// </summary>
    public PlayerController GetPlayerIfOnPiece(MapPiece piece)
    {
        if (currentPiece != piece) return null;
        return player;
    }

    void DetectAndReparent()
    {
        if (isParentLocked) return;
        if (isFlipping) return; // 플립 중에는 부모 변경 금지

        MapPiece dragging = MapPieceSelector.Instance.DraggingPiece;

        // 발 아래 감지 (MapPiece와 MovingPlatform 동시에)
        RaycastHit2D hit = GetGroundHit();

        MovingPlatform groundMovingPlatform = hit.collider != null
            ? hit.collider.GetComponentInParent<MovingPlatform>()
            : null;

        MapPiece groundPiece = hit.collider != null
            ? hit.collider.GetComponentInParent<MapPiece>()
            : null;

        // 드래그 중이더라도 현재 소속 조각 영역을 벗어나면 재판정
        if (dragging != null)
        {
            if (groundPiece == dragging)
            {
                if (groundMovingPlatform != null)
                    SetParent(groundMovingPlatform.transform, groundPiece);
                else if (groundPiece != currentPiece)
                    SetParent(groundPiece.transform, groundPiece);
                return;
            }
            return;
        }

        // 발 아래 MovingPlatform 우선
        if (groundMovingPlatform != null)
        {
            SetParent(groundMovingPlatform.transform, groundPiece);
            return;
        }

        // 드래그 중이 아닐 때는 발 아래 조각 기준
        MapPiece detected = groundPiece;

        // 발 아래 조각이 없으면 영역 기준으로 fallback
        if (detected == null)
        {
            foreach (var piece in GameManager.Instance.AllPieces)
            {
                if (piece.ContainsPlayer(player.transform.position))
                {
                    detected = piece;
                    break;
                }
            }
        }

        if (detected != null)
            SetParent(detected.transform, detected);
        else
            SetParent(sceneRoot, null);
    }

    RaycastHit2D GetGroundHit()
    {
        BoxCollider2D playerCol = player.Col;
        Vector2 origin = (Vector2)player.transform.position
                       + Vector2.down * (playerCol.size.y * 0.5f + 0.05f);
        return Physics2D.Raycast(origin, Vector2.down, 0.2f,
            LayerMask.GetMask("Platform"));
    }

    // 발 아래 플랫폼의 소속 MapPiece 반환
    MapPiece GetGroundPiece()
    {
        BoxCollider2D playerCol = player.Col;
        Vector2 origin = (Vector2)player.transform.position + Vector2.down * (playerCol.size.y * 0.5f + 0.05f);

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            0.2f,
            LayerMask.GetMask("Platform")
        );

        if (hit.collider == null) return null;

        // 해당 플랫폼의 부모 중 MapPiece 찾기
        return hit.collider.GetComponentInParent<MapPiece>();
    }

    void SetParent(Transform newParent, MapPiece piece)
    {
        if (player.transform.parent == newParent) return;

        currentPiece = piece;
        lastPiecePosition = piece != null
            ? (Vector2)piece.transform.position
            : Vector2.zero;
        player.transform.SetParent(newParent, worldPositionStays: true);

        // 부모 회전에 영향받지 않도록 월드 rotation 고정
        player.transform.rotation = Quaternion.identity;

        // 부모의 누적 scale을 역보정
        Vector3 parentScale = newParent != sceneRoot
            ? newParent.lossyScale
            : Vector3.one;

        player.transform.localScale = new Vector3(
            Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f,
            Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f,
            1f);

        // 부모가 바뀌면 OutlineObject가 ownerPiece를 다시 탐색하도록 갱신
        foreach (var outline in player.GetComponentsInChildren<OutlineObject>())
            outline.RefreshOwnerPiece();
    }

    void TrackPieceMovement()
    {
        // 플립 중에는 겹침 해소를 건너뜀
        if (isFlipping) return;

        // 다른 맵 조각과의 겹침을 매 프레임 해소
        player.ResolveExternalOverlap(currentPiece);
    }
}