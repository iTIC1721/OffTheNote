using System;
using UnityEngine;

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
    [SerializeField] private GameObject immovableObject;

    [Header("Pin")]
    [SerializeField] private bool isPinned = false;
    [SerializeField] private Vector2 pinLocalPosition = Vector2.zero;
    [SerializeField] private GameObject pinMarkerPrefab; // 회색 원 프리팹

    public bool IsMovable => isMovable;
    public bool IsPinned => isPinned;
    public void SetMovable(bool movable) => isMovable = movable;

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
        sr.color = MapPieceManager.Instance.color;
        sr.sprite = isMovable ? MapPieceManager.Instance.movableSprite : MapPieceManager.Instance.immovableSprite;
        sr.size = pieceArea.size + Vector2.one * MapPieceManager.Instance.mapPiecePadding;

        originalSortingOrder = isMovable ? 0 : -2;
        SetSortingOrder(originalSortingOrder);

        lastPosition = rb.position;

        SetupImmovableObject();

        if (isPinned)
            SetupPin(isPinned, pinLocalPosition);
    }

    // ── 마우스 입력 ────────────────────────────────────────────

    public void StartDrag()
    {
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

        if (!isMovable) return;

        isDragging = true;
        Vector2 mouseWorld = GetMouseWorldPos();
        dragOffset = rb.position - mouseWorld;
        SetSortingOrder(MapPieceManager.Instance.sortingOrderWhenDragged);
    }

    public void StopDrag()
    {
        if (isPinDragging)
        {
            isPinDragging = false;

            // 가장 가까운 90도
            float snapped = Mathf.Round(currentAngle / 90f) * 90f;

            if (!IsRotationBlocked(snapped))
            {
                targetAngle = snapped;
            }
            else
            {
                // 충돌 시 반대쪽(이전) 90도로 스냅
                // snapped가 currentAngle보다 크면 반시계, 작으면 시계 방향으로 되돌림
                float fallback = snapped > currentAngle
                    ? snapped - 90f
                    : snapped + 90f;

                if (!IsRotationBlocked(fallback))
                    targetAngle = fallback;
                else
                    targetAngle = currentAngle; // 양쪽 다 막히면 현재 유지
            }
            return;
        }

        isDragging = false;
        rb.linearVelocity = Vector2.zero;
        SetSortingOrder(originalSortingOrder);
    }

    // ── 매 프레임마다 회전 업데이트 ──────────────────────
    void Update()
    {
        if (isPinned)
            UpdatePinRotation();
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
                currentAngle = targetAngle;
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

                if (Mathf.Abs(axisDelta.x) > 0.001f || Mathf.Abs(axisDelta.y) > 0.001f)
                {
                    float shrink = 0.01f;
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

                        if (hit.distance <= 0.01f)
                        {
                            float dot = axisIdx == 0
                                ? resolvedDelta.x * hit.normal.x
                                : resolvedDelta.y * hit.normal.y;

                            if (dot < 0)
                            {
                                if (axisIdx == 0) resolvedDelta.x = 0;
                                else resolvedDelta.y = 0;
                            }
                            continue;
                        }

                        float allowed = Mathf.Max(0, hit.distance - 0.01f);
                        if (axisIdx == 0 && allowed < Mathf.Abs(resolvedDelta.x))
                            resolvedDelta.x = allowed * Mathf.Sign(resolvedDelta.x);
                        else if (axisIdx == 1 && allowed < Mathf.Abs(resolvedDelta.y))
                            resolvedDelta.y = allowed * Mathf.Sign(resolvedDelta.y);
                    }
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
        if (immovableObject != null && !visible)
            immovableObject.SetActive(false);
    }

    void SetSortingOrder(int order)
    {
        sr.sortingOrder = order;
    }

    void SetupImmovableObject()
    {
        if (immovableObject == null) return;

        immovableObject.SetActive(sr.enabled && !isMovable && !isPinned);
        if (isMovable || isPinned) return;

        Vector2 pieceSize = pieceArea.size;
        Vector3 parentScale = transform.localScale;
        float scaleX = Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f;
        float scaleY = Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f;

        float sizeRatio = pieceSize.x * 0.25f;
        immovableObject.transform.localScale = new Vector3(scaleX * sizeRatio, scaleY * sizeRatio, 1f);

        // MapPiece 위쪽 경계선으로 이동
        // pieceArea.offset은 콜라이더 중심 오프셋, pieceSize.y * 0.5f는 위쪽 경계까지의 거리
        float topY = pieceArea.offset.y + pieceSize.y * 0.5f + MapPieceManager.Instance.mapPiecePadding * 0.5f;
        immovableObject.transform.localPosition = new Vector3(pieceArea.offset.x, topY, 0f);
    }

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