using System;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GridBrushBase;

/// <summary>
/// 드래그 가능한 맵 조각.
/// 
/// 구조:
///   MapPiece (이 컴포넌트 + Rigidbody2D Kinematic + 별도 콜라이더 없음)
///   └── Platform_A (SpriteRenderer + BoxCollider2D, Layer = "Platform")
///   └── Platform_B ...
///   └── ...
/// 
/// 맵 조각 자체에는 콜라이더를 붙이지 않는다.
/// 충돌은 자식 플랫폼들이 각각 처리한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MapPiece : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private bool isMovable = true;

    [Header("Immovable")]
    [SerializeField] private Color movableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color immovableColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Header("Pin")]
    [SerializeField] private bool isPinned = false;
    [SerializeField] private Vector2 pinLocalPosition = Vector2.zero;
    [SerializeField] private GameObject pinMarkerPrefab; // 회색 원 프리팹

    [Header("Flip")]
    [SerializeField] private bool isFlippable = false;
    [SerializeField] private FlipAxis flipAxis = FlipAxis.X; // X축 뒤집기(상하) or Y축 뒤집기(좌우)
    [SerializeField] private GameObject flipMarkerPrefab; // 양방향 화살표 프리팹

    public bool IsMovable => isMovable;
    public bool IsPinned => isPinned;
    public bool IsFlippable => isFlippable;
    public bool IsFixed => !isMovable && !isPinned && !isFlippable;
    public void SetMovable(bool movable)
    {
        isMovable = movable;
        if (sr != null) RefreshColor();
    }

    /// <summary>맵 조각 자체의 면적 (BoxCollider2D.size 기준).</summary>
    public float GetPlatformArea()
    {
        if (pieceArea == null) return 0f;
        return pieceArea.size.x * pieceArea.size.y;
    }

    /// <summary>
    /// MapPieceManager.RefreshSortingOrders()에서 호출.
    /// 드래그 중이 아닐 때만 즉시 반영하고, originalSortingOrder도 갱신한다.
    /// </summary>
    public void SetBaseSortingOrder(int order)
    {
        originalSortingOrder = order;
        if (!isDragging)
            SetSortingOrder(originalSortingOrder);
    }

    /// <summary>
    /// flip 축 방향의 현재 scale 계수 (cos 곡선: 1 → 0 → -1).
    /// OutlineObject가 flip 축만 역보정을 해제할 때 참조한다.
    /// flip 기능이 없는 조각은 항상 1을 반환.
    /// </summary>
    public float FlipScaleMultiplier
    {
        get
        {
            if (!isFlippable) return 1f;
            float t = flipProgress / 180f;
            return Mathf.Cos(t * Mathf.PI);
        }
    }

    /// <summary>flip 축 (X = 상하반전, Y = 좌우반전)</summary>
    public FlipAxis FlipAxisValue => flipAxis;

    // ── 내부 상태 ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Camera mainCam;
    private bool isDragging = false;
    private Vector2 dragOffset;          // 마우스 클릭 지점과 오브젝트 중심의 차이
    private int originalSortingOrder = 0;

    private Vector2 lastPosition;

    private BoxCollider2D pieceArea; // 맵 조각 전체 영역 콜라이더 (클릭용 BoxCollider2D 재사용 가능)

    // 핀 회전 상태
    private float currentAngle = 0f;   // 현재 실제 각도
    private float targetAngle = 0f;   // 목표 스냅 각도 (0, 90, 180, 270)
    private float dragStartAngle = 0f;   // 드래그 시작 시 각도
    private Vector2 dragStartMousePos = Vector2.zero;
    private bool isPinDragging = false;
    private GameObject pinMarker;

    private const float SNAP_SPEED = 8f;   // 스냅 보간 속도
    private const float SNAP_THRESHOLD = 30f; // 스냅 임계 각도 (도)

    // 플립 상태
    // flipProgress: 0 = 원본, 180 = 완전히 뒤집힘 (scale -1)
    // 실제 scale 변환: axis==X → scaleY *= cos(flipProgress), axis==Y → scaleX *= cos(flipProgress)
    private float flipProgress = 0f;      // 현재 플립 진행 각도 (0 ~ 180)
    private float flipTargetProgress = 0f; // 목표 스냅 값 (0 or 180)
    private float flipDragStart = 0f;      // 드래그 시작 시 flipProgress
    private Vector2 flipDragStartMousePos = Vector2.zero;
    private bool isFlipDragging = false;
    private bool isFlipped = false;        // 현재 뒤집힌 상태 여부
    private float flipDragReferenceDistance = 1f;
    private GameObject flipMarker;

    // 플립 중 플레이어 추적
    // 플레이어를 분리하지 않고 로컬 좌표를 flip에 맞춰 매 프레임 보정
    private PlayerController flipTrackedPlayer = null; // 플립 중 위치를 추적할 플레이어

    // 플립 드래그 감도: 이 픽셀만큼 마우스를 움직이면 180도 플립
    private const float FLIP_DRAG_PIXELS = 200f;
    private const float FLIP_SNAP_SPEED = 8f;
    private const float FLIP_MAX_DEG_PER_SEC = 360f;

    // ── 그리드 스냅 (선택사항) ─────────────────────────────────
    [Header("Grid Snap (0 = off)")]
    [SerializeField] private float snapSize = 0f;

    public bool ContainsPlayer(Vector2 playerPos)
    {
        if (pieceArea == null) return false;
        return pieceArea.OverlapPoint(playerPos);
    }

    public Vector2 GetVelocity() => rb.linearVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;   // 중력 없이 마우스로만 움직임
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        pieceArea = GetComponent<BoxCollider2D>();

        sr = GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Sliced;

        mainCam = Camera.main;
    }

    private void Start()
    {
        RefreshColor();
        sr.sprite = isMovable ? MapPieceManager.Instance.movableSprite : MapPieceManager.Instance.immovableSprite;
        sr.size = pieceArea.size + Vector2.one * MapPieceManager.Instance.mapPiecePadding;

        // sortingOrder는 MapPieceManager.Start()에서 RefreshSortingOrders()로 일괄 할당된다.
        SetSortingOrder(originalSortingOrder);

        lastPosition = rb.position;

        //SetupImmovableObject();

        if (isPinned)
            SetupPin(isPinned, pinLocalPosition);

        if (isFlippable)
            SetupFlipMarker();
    }

    // ── 마우스 입력 ────────────────────────────────────────────

    public void StartDrag()
    {
        // 조작 시작 즉시 이 조각을 최상위로 올린다
        MapPieceManager.Instance?.RefreshSortingOrders(this);
        SetChildOutlineHighlight(true);

        if (isPinned)
        {
            // 핀 모드: 드래그로 회전
            isPinDragging = true;
            dragStartAngle = currentAngle;
            dragStartMousePos = GetMouseWorldPos();

            // 플레이어가 이 조각 소속이면 분리
            MapPieceManager.Instance?.DetachPlayerFromPiece(this);
            return;
        }

        if (isFlippable)
        {
            // 플립 모드: 드래그로 뒤집기
            isFlipDragging = true;
            flipDragStart = flipProgress;
            flipDragStartMousePos = GetMouseWorldPos();

            float axisOffset = flipAxis == FlipAxis.Y
                ? flipDragStartMousePos.x - transform.position.x
                : flipDragStartMousePos.y - transform.position.y;
            flipDragReferenceDistance = Mathf.Max(Mathf.Abs(axisOffset), 0.5f);

            // Platform 콜라이더 비활성화
            // → 조각 위에 있던 플레이어는 부모-자식 관계로 scale/position이 자동 전파됨
            // → 외부 플레이어는 조각을 통과해 낙하 → 리스폰
            SetPlatformCollidersEnabled(false);

            // 플레이어가 이 조각 위에 있으면 조작만 막는다
            // 위치/scale은 부모(이 조각 또는 자식 MovingPlatform)의 transform이 자동으로 전파
            PlayerController trackedPlayer = MapPieceManager.Instance?.GetPlayerIfOnPiece(this);
            if (trackedPlayer != null)
            {
                flipTrackedPlayer = trackedPlayer;
                trackedPlayer.SetControllable(false);
            }

            MapPieceManager.Instance?.SetFlipping(true);

            if (!isMovable) return;
        }

        if (!isMovable) return;

        isDragging = true;
        Vector2 mouseWorld = GetMouseWorldPos();
        dragOffset = rb.position - mouseWorld;
    }

    public void StopDrag()
    {
        bool wasActive = isPinDragging || isFlipDragging || isDragging;

        if (isPinDragging)
        {
            isPinDragging = false;

            // 가장 가까운 90도
            float snapped = Mathf.Round(currentAngle / 90f) * 90f;

            if (!IsRotationPathBlocked(dragStartAngle, snapped))
            {
                targetAngle = snapped;
            }
            else
            {
                // 충돌 시 반대쪽(이전) 90도로 스냅
                // snapped가 currentAngle보다 크면 반시계, 작으면 시계 방향으로 되돌림
                float fallback = Mathf.DeltaAngle(currentAngle, snapped) > 0f
                    ? snapped - 90f
                    : snapped + 90f;

                if (!IsRotationPathBlocked(dragStartAngle, fallback))
                    targetAngle = fallback;
                else
                    targetAngle = currentAngle; // 양쪽 다 막히면 현재 유지
            }
            MapPieceManager.Instance?.RefreshSortingOrders(null);
            SetChildOutlineHighlight(false);
            return;
        }

        if (isFlipDragging)
        {
            isFlipDragging = false;

            // 가장 가까운 180도 배수로 스냅 (무한 회전 지원)
            float snapped = Mathf.Round(flipProgress / 180f) * 180f;
            float normalizedSnapped = ((snapped % 360f) + 360f) % 360f;
            bool snappedIsFlipped = normalizedSnapped >= 90f && normalizedSnapped < 270f;

            if (!IsFlipBlocked(snappedIsFlipped))
            {
                flipTargetProgress = snapped;
            }
            else
            {
                // 막히면 반대쪽 180도 배수로 스냅
                float fallback = snapped > flipProgress ? snapped - 180f : snapped + 180f;
                float normalizedFallback = ((fallback % 360f) + 360f) % 360f;
                bool fallbackIsFlipped = normalizedFallback >= 90f && normalizedFallback < 270f;
                if (!IsFlipBlocked(fallbackIsFlipped))
                    flipTargetProgress = fallback;
                else
                    flipTargetProgress = flipProgress; // 현재 상태 유지
            }

            // isDragging도 같이 종료
            if (isDragging)
            {
                isDragging = false;
                rb.linearVelocity = Vector2.zero;
                MapPieceManager.Instance?.RefreshSortingOrders(null);
            }

            // flipProgress가 이미 목표값에 도달해 있으면 UpdateFlip의 justFinished가
            // 발화되지 않으므로 여기서 즉시 복원한다.
            if (Mathf.Approximately(flipProgress, flipTargetProgress))
                FinishFlip();

            // 플레이어 조작 복원은 스냅 완료(UpdateFlip) 시점에 처리
            return;
        }

        isDragging = false;
        rb.linearVelocity = Vector2.zero;
        MapPieceManager.Instance?.RefreshSortingOrders(null);
        SetChildOutlineHighlight(false);
    }

    // ── 매 프레임마다 회전 업데이트 ──────────────────────
    void Update()
    {
        if (isPinned)
            UpdatePinRotation();

        if (isFlippable)
            UpdateFlip();
    }

    void UpdatePinRotation()
    {
        if (isPinDragging)
        {
            Vector2 mouseWorld = GetMouseWorldPos();
            Vector2 pinWorld = (Vector2)transform.position
                               + (Vector2)(transform.rotation * pinLocalPosition);

            float startAng = Mathf.Atan2(
                dragStartMousePos.y - pinWorld.y,
                dragStartMousePos.x - pinWorld.x) * Mathf.Rad2Deg;
            float currAng = Mathf.Atan2(
                mouseWorld.y - pinWorld.y,
                mouseWorld.x - pinWorld.x) * Mathf.Rad2Deg;

            float delta = Mathf.DeltaAngle(startAng, currAng);
            float rawTarget = dragStartAngle + delta;

            if (!IsRotationBlocked(rawTarget))
            {
                targetAngle = rawTarget;

                float angleDelta = Mathf.DeltaAngle(currentAngle, targetAngle);
                float stepSize = 3f;
                int steps = Mathf.CeilToInt(Mathf.Abs(angleDelta) / stepSize);
                float sign = Mathf.Sign(angleDelta);

                float safeAngle = currentAngle;
                for (int i = 1; i <= steps; i++)
                {
                    float candidate = currentAngle + sign * Mathf.Min(stepSize * i, Mathf.Abs(angleDelta));
                    if (IsRotationBlocked(candidate)) break;
                    safeAngle = candidate;
                }

                currentAngle = safeAngle;
            }
            else
            {
                // 막힌 경우 현재 위치를 새 기준점으로 갱신
                // 다음 프레임부터 현재 각도 기준으로 delta 계산
                dragStartAngle = currentAngle;
                dragStartMousePos = mouseWorld;
            }
        }
        else
        {
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * SNAP_SPEED);
        }

        ApplyPinRotation();
    }

    bool IsRotationBlocked(float angle)
    {
        Vector3 savedPos = transform.position;
        Quaternion savedRot = transform.rotation;

        // 회전 시뮬레이션
        Vector2 pinWorld = (Vector2)transform.position
                         + (Vector2)(transform.rotation * pinLocalPosition);
        transform.rotation = Quaternion.Euler(0, 0, angle);
        Vector2 pinWorldAfter = (Vector2)transform.position
                              + (Vector2)(transform.rotation * pinLocalPosition);
        transform.position += (Vector3)(pinWorld - pinWorldAfter);

        bool blocked = false;
        var platforms = GetComponentsInChildren<BoxCollider2D>();
        int platformLayer = LayerMask.NameToLayer("Platform");
        int blockerLayer = LayerMask.NameToLayer("Blocker");
        int checkMask = LayerMask.GetMask("Platform", "Blocker");
        float shrink = 0.05f;

        foreach (var col in platforms)
        {
            if (col.gameObject.layer != platformLayer &&
                col.gameObject.layer != blockerLayer) continue;

            // bounds 대신 transform 기준으로 직접 월드 위치 계산
            Vector2 colWorldCenter = (Vector2)col.transform.TransformPoint(col.offset);

            // 콜라이더 크기도 transform scale 반영
            Vector2 colWorldSize = new Vector2(
                col.size.x * Mathf.Abs(col.transform.lossyScale.x),
                col.size.y * Mathf.Abs(col.transform.lossyScale.y));

            float colAngle = col.transform.eulerAngles.z;

            Vector2 size = new Vector2(
                Mathf.Max(0.01f, colWorldSize.x - shrink),
                Mathf.Max(0.01f, colWorldSize.y - shrink));

            Collider2D[] hits = Physics2D.OverlapBoxAll(
                colWorldCenter, size, colAngle, checkMask);

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                blocked = true;
                break;
            }
            if (blocked) break;
        }

        // 원래 상태로 복원
        transform.position = savedPos;
        transform.rotation = savedRot;

        return blocked;
    }

    bool IsRotationPathBlocked(float fromAngle, float toAngle, float stepDeg = 5f)
    {
        float delta = Mathf.DeltaAngle(fromAngle, toAngle);
        float sign = Mathf.Sign(delta);
        float travelled = 0f;

        while (Mathf.Abs(travelled) < Mathf.Abs(delta))
        {
            float step = sign * Mathf.Min(stepDeg, Mathf.Abs(delta) - Mathf.Abs(travelled));
            travelled += step;
            float angle = fromAngle + travelled;
            if (IsRotationBlocked(angle)) return true;
        }
        return false;
    }

    void ApplyPinRotation()
    {
        // 회전 전 핀의 월드 좌표 저장
        Vector2 pinWorldBefore = (Vector2)transform.position
                               + (Vector2)(transform.rotation * pinLocalPosition);

        // 회전 적용
        transform.rotation = Quaternion.Euler(0, 0, currentAngle);

        // 회전 후 핀 월드 좌표가 변했으면 position으로 보정
        Vector2 pinWorldAfter = (Vector2)transform.position
                              + (Vector2)(transform.rotation * pinLocalPosition);
        transform.position += (Vector3)(pinWorldBefore - pinWorldAfter);

        // Rigidbody 동기화
        rb.MovePosition(transform.position);
        rb.MoveRotation(currentAngle);
    }

    // ── 플립 ──────────────────────────────────────────────────

    void UpdateFlip()
    {
        if (isFlipDragging)
        {
            Vector2 mouseWorld = GetMouseWorldPos();
            Vector2 mouseDelta = mouseWorld - flipDragStartMousePos;

            // 축에 따라 드래그 방향 결정
            // FlipAxis.X (상하 뒤집기): Y축 드래그
            // FlipAxis.Y (좌우 뒤집기): X축 드래그
            float dragAmount = flipAxis == FlipAxis.X ? -mouseDelta.y : mouseDelta.x;

            // 월드 단위 → 플립 각도 변환 (FLIP_DRAG_PIXELS 월드유닛 = 180도)
            float worldUnitsPerHalfFlip = flipDragReferenceDistance * Mathf.PI;
            float deltaAngle = (dragAmount / worldUnitsPerHalfFlip) * 180f;

            float raw = flipDragStart + deltaAngle;

            // 목표 상태(뒤집힘 여부): 누적 각도를 360으로 나눈 나머지 기준
            float normalizedRaw = ((raw % 360f) + 360f) % 360f;
            bool wouldBeFlipped = normalizedRaw >= 90f && normalizedRaw < 270f;
            if (!IsFlipBlocked(wouldBeFlipped))
            {
                flipProgress = Mathf.MoveTowards(
                    flipProgress,
                    raw,
                    FLIP_MAX_DEG_PER_SEC * Time.deltaTime);
            }
            else
            {
                // 막힌 경우 현재 위치를 새 기준으로 갱신
                flipDragStart = flipProgress;
                flipDragStartMousePos = mouseWorld;
            }

            flipTargetProgress = flipProgress;
        }
        else
        {
            // 스냅 보간
            float prev = flipProgress;
            flipProgress = Mathf.MoveTowards(
                flipProgress,
                flipTargetProgress,
                FLIP_SNAP_SPEED * 180f * Time.deltaTime);

            // 스냅 완료 시 복원
            // prev != flipTargetProgress 조건으로 "이번 프레임에 도달"한 경우만 처리
            if (prev != flipTargetProgress && Mathf.Approximately(flipProgress, flipTargetProgress))
                FinishFlip();
        }

        ApplyFlip();
    }

    void ApplyFlip()
    {
        // flipProgress 0~180을 scale 변환으로 표현
        // 0도: scale=1, 90도: scale=0(가장 얇음), 180도: scale=-1(뒤집힘)
        float t = flipProgress / 180f;
        float scaleFactor = Mathf.Cos(t * Mathf.PI); // 1 → 0 → -1

        Vector3 s = transform.localScale;
        if (flipAxis == FlipAxis.X)
            s.y = scaleFactor;
        else
            s.x = scaleFactor;

        transform.localScale = s;

        isFlipped = (((flipProgress % 360f) + 360f) % 360f) >= 90f &&
                    (((flipProgress % 360f) + 360f) % 360f) < 270f;
    }

    /// <summary>
    /// 뒤집힌 상태에서 자식 콜라이더들이 다른 Platform/Blocker와 겹치는지 검사
    /// </summary>
    bool IsFlipBlocked(bool flippedState)
    {
        Vector3 savedScale = transform.localScale;

        // 뒤집힌 상태의 scale 적용
        Vector3 testScale = savedScale;
        if (flipAxis == FlipAxis.X)
            testScale.y = flippedState ? -1f : 1f;
        else
            testScale.x = flippedState ? -1f : 1f;

        transform.localScale = testScale;
        Physics2D.SyncTransforms();

        bool blocked = false;
        var platforms = GetComponentsInChildren<BoxCollider2D>();
        int platformLayer = LayerMask.NameToLayer("Platform");
        int blockerLayer = LayerMask.NameToLayer("Blocker");
        int checkMask = LayerMask.GetMask("Platform", "Blocker");
        float shrink = 0.05f;

        foreach (var col in platforms)
        {
            if (col.gameObject.layer != platformLayer &&
                col.gameObject.layer != blockerLayer) continue;

            Vector2 colWorldCenter = (Vector2)col.transform.TransformPoint(col.offset);
            Vector2 colWorldSize = new Vector2(
                col.size.x * Mathf.Abs(col.transform.lossyScale.x),
                col.size.y * Mathf.Abs(col.transform.lossyScale.y));

            float colAngle = col.transform.eulerAngles.z;
            Vector2 size = new Vector2(
                Mathf.Max(0.01f, colWorldSize.x - shrink),
                Mathf.Max(0.01f, colWorldSize.y - shrink));

            Collider2D[] hits = Physics2D.OverlapBoxAll(colWorldCenter, size, colAngle, checkMask);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                blocked = true;
                break;
            }
            if (blocked) break;
        }

        // 복원
        transform.localScale = savedScale;
        Physics2D.SyncTransforms();

        return blocked;
    }

    /// <summary>
    /// 플립 완료 시 공통 정리: 콜라이더 복원, 플레이어 조작 복원, 플립 플래그 해제.
    /// StopDrag(즉시 완료)와 UpdateFlip(스냅 보간 완료) 두 경로에서 모두 호출된다.
    /// </summary>
    void FinishFlip()
    {
        SetPlatformCollidersEnabled(true);

        if (flipTrackedPlayer != null)
        {
            flipTrackedPlayer.SetControllable(true);
            flipTrackedPlayer = null;
        }

        MapPieceManager.Instance?.SetFlipping(false);
        SetChildOutlineHighlight(false);
    }

    /// <summary>
    /// 플립 중 외부 플레이어의 진입을 막기 위해 Platform 자식 콜라이더를 켜고 끈다.
    /// 비활성화된 동안 외부 플레이어는 조각을 통과해 낙하(→ 리스폰)한다.
    /// 추적 중인 내부 플레이어는 ApplyFlip이 transform.position으로 직접 제어하므로
    /// 콜라이더 상태와 무관하게 올바른 위치에 배치된다.
    /// </summary>
    void SetPlatformCollidersEnabled(bool enable)
    {
        int platformLayer = LayerMask.NameToLayer("Platform");
        foreach (var col in GetComponentsInChildren<BoxCollider2D>())
        {
            if (col.gameObject.layer == platformLayer)
                col.enabled = enable;
        }
    }

    // ── 매 물리 프레임마다 위치 업데이트 ──────────────────────
    void FixedUpdate()
    {
        if (!isDragging) return;

        Vector2 target = GetMouseWorldPos() + dragOffset;
        Vector2 nextPos = Vector2.Lerp(rb.position, target, MapPieceManager.Instance.dragSmoothSpeed * Time.fixedDeltaTime);

        // 최대 속도 제한
        Vector2 delta = nextPos - rb.position;
        float maxDelta = MapPieceManager.Instance.maxSpeed * Time.fixedDeltaTime;
        if (delta.magnitude > maxDelta)
            nextPos = rb.position + delta.normalized * maxDelta;

        // 카메라 범위 클램핑
        nextPos = ClampToCamera(nextPos);

        // 플랫폼 충돌 검사
        delta = nextPos - rb.position;
        delta = ResolvePlatformCollision(delta);
        nextPos = rb.position + delta;

        rb.linearVelocity = (nextPos - rb.position) / Time.fixedDeltaTime;
        rb.MovePosition(nextPos);
    }

    Vector2 ClampToCamera(Vector2 nextPos)
    {
        Camera cam = Camera.main;
        float camHeight = cam.orthographicSize;
        float camWidth = cam.orthographicSize * cam.aspect;
        Vector2 camPos = cam.transform.position;

        float minX = camPos.x - camWidth;
        float maxX = camPos.x + camWidth;
        float minY = camPos.y - camHeight;
        float maxY = camPos.y + camHeight;

        BoxCollider2D[] myPlatforms = GetComponentsInChildren<BoxCollider2D>();
        int platformLayer = LayerMask.NameToLayer("Platform");

        Vector2 correction = Vector2.zero;

        foreach (var platform in myPlatforms)
        {
            if (platform.gameObject.layer != platformLayer) continue;

            // 이동 후 플랫폼의 예상 bounds
            Vector2 platformCenter = (Vector2)platform.bounds.center + (nextPos - rb.position);
            Vector2 halfSize = platform.bounds.extents;

            float overLeft = minX - (platformCenter.x - halfSize.x);
            float overRight = (platformCenter.x + halfSize.x) - maxX;
            float overBottom = minY - (platformCenter.y - halfSize.y);
            float overTop = (platformCenter.y + halfSize.y) - maxY;

            if (overLeft > 0) correction.x = Mathf.Max(correction.x, overLeft);
            if (overRight > 0) correction.x = Mathf.Min(correction.x, -overRight);
            if (overBottom > 0) correction.y = Mathf.Max(correction.y, overBottom);
            if (overTop > 0) correction.y = Mathf.Min(correction.y, -overTop);
        }

        return nextPos + correction;
    }

    Vector2 ResolvePlatformCollision(Vector2 delta)
    {
        BoxCollider2D[] myPlatforms = GetComponentsInChildren<BoxCollider2D>();
        Vector2 resolvedDelta = delta;

        int platformLayer = LayerMask.NameToLayer("Platform");
        int blockerLayer = LayerMask.NameToLayer("Blocker");
        int checkMask = LayerMask.GetMask("Platform", "Blocker");

        for (int axisIdx = 0; axisIdx < 2; axisIdx++)
        {
            Vector2 axisDelta = axisIdx == 0
                ? new Vector2(resolvedDelta.x, 0)
                : new Vector2(0, resolvedDelta.y);

            if (axisDelta.magnitude <= 0.001f) continue;

            foreach (var platform in myPlatforms)
            {
                if (platform.gameObject.layer != platformLayer &&
                    platform.gameObject.layer != blockerLayer) continue;

                Vector2 platformPos = platform.bounds.center;
                Vector2 platformSize = platform.bounds.size;

                float shrink = 0.1f;
                Vector2 shrunkSize = new Vector2(
                    Mathf.Max(0.01f, platformSize.x - shrink),
                    Mathf.Max(0.01f, platformSize.y - shrink));

                RaycastHit2D[] hits = Physics2D.BoxCastAll(
                    platformPos,
                    shrunkSize,
                    0f,
                    axisDelta.normalized,
                    axisDelta.magnitude + 0.01f,
                    checkMask
                );

                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue;

                    if (hit.distance <= 0f)
                    {
                        float dot = Vector2.Dot(axisDelta.normalized, hit.normal);
                        if (dot >= 0f) continue;
                    }

                    float allowed = Mathf.Max(0f, hit.distance - 0.01f);

                    if (axisIdx == 0 && allowed < Mathf.Abs(resolvedDelta.x))
                        resolvedDelta.x = allowed * Mathf.Sign(resolvedDelta.x);
                    else if (axisIdx == 1 && allowed < Mathf.Abs(resolvedDelta.y))
                        resolvedDelta.y = allowed * Mathf.Sign(resolvedDelta.y);
                }

                if (axisIdx == 0 && Mathf.Abs(resolvedDelta.x) <= 0.001f) break;
                if (axisIdx == 1 && Mathf.Abs(resolvedDelta.y) <= 0.001f) break;
            }
        }

        return resolvedDelta;
    }

    // ── 유틸 ──────────────────────────────────────────────────

    Vector2 GetMouseWorldPos()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(mainCam.transform.position.z);
        return mainCam.ScreenToWorldPoint(mp);
    }

    Vector2 SnapToGrid(Vector2 pos)
    {
        return new Vector2(
            Mathf.Round(pos.x / snapSize) * snapSize,
            Mathf.Round(pos.y / snapSize) * snapSize
        );
    }

    public void SetVisible(bool visible)
    {
        sr.enabled = visible;

        // immovableObject도 visible이 꺼지면 숨김
        //if (immovableObject != null && !visible)
        //    immovableObject.SetActive(false);
    }

    void SetSortingOrder(int order)
    {
        sr.sortingOrder = order;
    }

    /// <summary>
    /// isMovable 상태에 따라 SpriteRenderer 색상을 갱신한다.
    /// movable이면 MapPieceManager.color, immovable이면 immovableColor를 적용한다.
    /// </summary>
    void RefreshColor()
    {
        sr.color = MapPieceManager.Instance.color * (!IsFixed ? movableColor : immovableColor);
    }

    //void SetupImmovableObject()
    //{
    //    if (immovableObject == null) return;

    //    immovableObject.SetActive(sr.enabled && !isMovable && !isPinned && !isFlippable);
    //    if (isMovable || isPinned || isFlippable) return;

    //    Vector2 pieceSize = pieceArea.size;
    //    Vector3 parentScale = transform.localScale;
    //    float scaleX = Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f;
    //    float scaleY = Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f;

    //    float sizeRatio = pieceSize.x * 0.25f;
    //    immovableObject.transform.localScale = new Vector3(scaleX * sizeRatio, scaleY * sizeRatio, 1f);

    //    // MapPiece 위쪽 경계선으로 이동
    //    // pieceArea.offset은 콜라이더 중심 오프셋, pieceSize.y * 0.5f는 위쪽 경계까지의 거리
    //    float topY = pieceArea.offset.y + pieceSize.y * 0.5f + MapPieceManager.Instance.mapPiecePadding * 0.5f;
    //    immovableObject.transform.localPosition = new Vector3(pieceArea.offset.x, topY, 0f);
    //}

    public void SetupPin(bool pinned, Vector2 pinLocal)
    {
        isPinned = pinned;
        pinLocalPosition = pinLocal;

        if (pinMarker != null) DestroyImmediate(pinMarker);

        if (isPinned && pinMarkerPrefab != null)
        {
            pinMarker = Instantiate(pinMarkerPrefab, transform);
            pinMarker.transform.localPosition = pinLocal;
        }
    }

    public void SetupFlip(bool flippable, FlipAxis axis, bool startFlipped = false)
    {
        isFlippable = flippable;
        flipAxis = axis;

        if (startFlipped)
        {
            flipProgress = 180f;
            flipTargetProgress = 180f;
            isFlipped = true;
            ApplyFlip();
        }
        else
        {
            flipProgress = 0f;
            flipTargetProgress = 0f;
            isFlipped = false;
            ApplyFlip();
        }
    }

    void SetupFlipMarker()
    {
        if (flipMarker != null) DestroyImmediate(flipMarker);

        if (isFlippable && flipMarkerPrefab != null)
        {
            flipMarker = Instantiate(flipMarkerPrefab, transform);
            flipMarker.transform.localPosition = Vector3.zero;
            flipMarker.transform.localRotation = Quaternion.Euler(0f, 0f, flipAxis == FlipAxis.X ? 90f : 0f);
        }
    }

    void SetChildOutlineHighlight(bool highlight)
    {
        GameObject playerGO = MapPieceManager.Instance?.GetPlayerIfOnPiece(this)?.gameObject;
        foreach (var outline in GetComponentsInChildren<OutlineObject>())
        {
            if (playerGO != null && outline.transform.IsChildOf(playerGO.transform)) continue;
            outline.SetHighlight(highlight);
        }
    }

    // ── 에디터 기즈모 ─────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 맵 조각 범위를 노란 박스로 표시
        Gizmos.color = Color.yellow;

        // 자식 콜라이더들의 bounds 합산
        var cols = GetComponentsInChildren<BoxCollider2D>();
        foreach (var c in cols)
        {
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
#endif
}

// MapPiece 뒤집기 축
public enum FlipAxis
{
    X, // X축 기준 뒤집기 (상하 반전, Y 스케일 반전)
    Y  // Y축 기준 뒤집기 (좌우 반전, X 스케일 반전)
}