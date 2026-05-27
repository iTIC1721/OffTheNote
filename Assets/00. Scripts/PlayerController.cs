using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float fallMultiplier = 2.5f;    // 낙하 시 중력 배수
    [SerializeField] private float lowJumpMultiplier = 2f;   // 짧은 점프 시 중력 배수

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Coyote Time")]
    [SerializeField] private float coyoteTime = 0.1f;
    private float coyoteTimer = 0f;

    private bool isControllable = true;

    public BoxCollider2D Col => col;
    private BoxCollider2D col;

    private Vector2 velocity; // 플레이어 velocity (중력, 점프, 이동)

    private bool isGrounded;
    private bool jumpQueued = false;

    private bool hasDoubleJump = false;
    private bool doubleJumpUsed = false;

    private Transform spawnPointTransform;
    private Vector2 spawnPosition; // fallback용

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
    }

    void Start()
    {
        if (spawnPoint != null)
            transform.position = spawnPoint.position;
    }

    void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        HandleJump();
    }

    void FixedUpdate()
    {
        HandleMove();
        CheckGrounded();

        Jump();
        Move();

        CheckOutOfBounds();
    }

    void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.BoxCast(
            transform.position,
            col.size,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        // 마스킹된 충돌은 착지로 인정하지 않음
        bool groundedThisFrame = hit.collider != null;

        if (groundedThisFrame)
        {
            coyoteTimer = coyoteTime;
            hasDoubleJump = false;  // 착지 시 2단 점프 초기화
            doubleJumpUsed = false;
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        isGrounded = groundedThisFrame;
    }

    void HandleMove()
    {
        if (!isControllable)
        {
            velocity.x = 0;
            return;
        }
        float h = Input.GetAxisRaw("Horizontal");
        velocity.x = h * moveSpeed;
    }

    void HandleJump()
    {
        if (!isControllable) return;

        if (Input.GetKeyDown(KeyCode.W))
        {
            if (coyoteTimer > 0f)
                jumpQueued = true;
            else if (!isGrounded && hasDoubleJump && !doubleJumpUsed)
            {
                velocity.y = jumpForce;
                doubleJumpUsed = true;
            }
        }
    }

    public void GiveDoubleJump()
    {
        hasDoubleJump = true;
        doubleJumpUsed = false;
    }

    void Move()
    {
        if (!isGrounded)
        {
            float currentGravity = gravity;

            if (velocity.y < 0)
                currentGravity *= fallMultiplier;           // 낙하 중 중력 강화
            else if (velocity.y > 0 && !Input.GetKey(KeyCode.W))
                currentGravity *= lowJumpMultiplier;        // 점프 키 떼면 빠르게 정점 도달

            velocity.y += currentGravity * Time.fixedDeltaTime;
        }

        Vector2 horizontalMove = new Vector2(velocity.x, 0) * Time.fixedDeltaTime;
        Vector2 resolvedH = ResolveCollision(horizontalMove);
        transform.position += (Vector3)resolvedH;

        // 수평 이동이 막혔으면 벽에서 살짝 밀어냄
        if (horizontalMove != Vector2.zero &&
            resolvedH.magnitude < horizontalMove.magnitude * 0.1f)
        {
            velocity.x = 0;
            // 벽 반대 방향으로 살짝 밀어냄
            transform.position += new Vector3(-Mathf.Sign(horizontalMove.x) * 0.01f, 0, 0);
        }

        Vector2 verticalMove = new Vector2(0, velocity.y) * Time.fixedDeltaTime;
        Vector2 resolvedV = ResolveCollision(verticalMove);
        transform.position += (Vector3)resolvedV;

        if (Mathf.Abs(resolvedV.y) < Mathf.Abs(verticalMove.y) * 0.95f)
            velocity.y = 0;

        ResolveOverlap();
    }

    void Jump()
    {
        if (jumpQueued)
        {
            if (coyoteTimer > 0f)
            {
                velocity.y = jumpForce;
                coyoteTimer = 0f;
            }
            jumpQueued = false; // 착지 여부와 무관하게 소비
        }
    }

    public void OnPieceMoved(Vector2 delta)
    {
        int steps = Mathf.CeilToInt(delta.magnitude / (col.size.y * 0.5f));
        steps = Mathf.Max(steps, 1);
        Vector2 stepDelta = delta / steps;

        for (int i = 0; i < steps; i++)
        {
            transform.position -= (Vector3)stepDelta;

            Vector2 resolvedH = ResolveCollision(new Vector2(stepDelta.x, 0));
            transform.position += (Vector3)resolvedH;

            Vector2 resolvedV = ResolveCollision(new Vector2(0, stepDelta.y));
            transform.position += (Vector3)resolvedV;

            ResolveOverlap();
        }
    }

    // 이동량을 받아 콜라이더 충돌을 고려한 실제 이동량을 반환
    Vector2 ResolveCollision(Vector2 moveAmount)
    {
        if (moveAmount == Vector2.zero) return Vector2.zero;

        float distance = moveAmount.magnitude;
        Vector2 direction = moveAmount.normalized;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            transform.position,
            col.size,
            0f,
            direction,
            distance + 0.001f,
            groundLayer
        );

        float allowedDistance = distance;
        foreach (var hit in hits)
        {
            if (Vector2.Dot(hit.normal, direction) > -0.5f) continue;

            float d = Mathf.Max(0, hit.distance - 0.001f);
            allowedDistance = Mathf.Min(allowedDistance, d);
        }

        return direction * allowedDistance;
    }

    void ResolveOverlap()
    {
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            transform.position,
            col.size,
            0f,
            groundLayer
        );

        foreach (var overlap in overlaps)
        {
            ColliderDistance2D dist = Physics2D.Distance(col, overlap);
            if (dist.isOverlapped)
            {
                Vector2 correction = dist.normal * dist.distance;
                correction.x = 0;
                transform.position += (Vector3)correction;

                if (dist.normal.y > 0.9f && velocity.y < 0)
                    velocity.y = 0;
            }
        }
    }

    public void ResolveExternalOverlap(MapPiece currentPiece)
    {
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            transform.position,
            col.size,
            0f,
            groundLayer
        );

        foreach (var overlap in overlaps)
        {
            if (currentPiece != null &&
                overlap.transform.IsChildOf(currentPiece.transform))
                continue;

            ColliderDistance2D dist = Physics2D.Distance(col, overlap);
            if (dist.isOverlapped)
            {
                transform.position += (Vector3)(dist.normal * dist.distance);
                if (dist.normal.y > 0.5f && velocity.y < 0)
                    velocity.y = 0;
            }
        }
    }

    // 플레이어 사망 로직
    void CheckOutOfBounds()
    {
        Camera cam = Camera.main;
        float camHeight = cam.orthographicSize;
        float camWidth = cam.orthographicSize * cam.aspect;
        Vector2 camPos = cam.transform.position;

        if (transform.position.x < camPos.x - camWidth ||
            transform.position.x > camPos.x + camWidth ||
            transform.position.y < camPos.y - camHeight ||
            transform.position.y > camPos.y + camHeight)
        {
            Respawn();
        }
    }

    public void SetSpawnPoint(Transform t)
    {
        spawnPointTransform = t;
    }

    public void Respawn()
    {
        Vector2 pos = spawnPointTransform != null
            ? (Vector2)spawnPointTransform.position
            : spawnPosition;

        transform.position = pos;
        velocity = Vector2.zero;
        hasDoubleJump = false;
        doubleJumpUsed = false;

        // 잠시 드래그 불가
        if (MapPieceSelector.Instance != null)
            MapPieceSelector.Instance.StartRespawnFreeze();
    }

    public void SetControllable(bool controllable)
    {
        isControllable = controllable;
        if (!controllable)
        {
            velocity = Vector2.zero;
            enabled = false;
        }
        else
        {
            enabled = true;
        }
    }
}