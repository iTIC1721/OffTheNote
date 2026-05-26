using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 움직이는 플랫폼 — 모든 이동을 localPosition 기준으로 처리
/// 부모 MapPiece가 이동해도 함께 따라감
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private MovingPlatformMode mode = MovingPlatformMode.PingPong;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 0f;

    [Header("PingPong")]
    [SerializeField] private Vector2 pointA = Vector2.zero;
    [SerializeField] private Vector2 pointB = new Vector2(3, 0);

    [Header("Loop")]
    [SerializeField] private List<Vector2> loopPoints = new List<Vector2>();

    [Header("Collision Reversal")]
    [SerializeField] private bool reverseOnCollision = true;
    [SerializeField] private float collisionShrink = 0.01f;

    // 초기 localPosition 기준 경유점 목록 (로컬 좌표)
    private List<Vector2> localPoints = new List<Vector2>();
    private Vector2 initialLocalPosition;

    private int currentTarget = 0;
    private int direction = 1;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    void Start()
    {
        initialLocalPosition = transform.localPosition;
        BuildLocalPoints();
        if (localPoints.Count > 0)
            transform.localPosition = localPoints[0];
    }

    void BuildLocalPoints()
    {
        localPoints.Clear();

        if (mode == MovingPlatformMode.PingPong)
        {
            localPoints.Add(initialLocalPosition + pointA);
            localPoints.Add(initialLocalPosition + pointB);
        }
        else
        {
            localPoints.Add(initialLocalPosition + pointA);
            foreach (var lp in loopPoints)
                localPoints.Add(initialLocalPosition + lp);
        }
    }

    void FixedUpdate()
    {
        if (localPoints.Count < 2) return;

        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f) isWaiting = false;
            return;
        }

        Vector2 currentLocal = transform.localPosition;
        Vector2 targetLocal = localPoints[currentTarget];
        float dist = Vector2.Distance(currentLocal, targetLocal);
        float step = speed * Time.fixedDeltaTime;

        // 이동 방향 계산
        Vector2 moveDir = (targetLocal - currentLocal).normalized;

        // 충돌 감지 후 방향 반전
        if (reverseOnCollision && IsBlocked(moveDir, step))
        {
            ReverseDirection();
            return;
        }

        if (dist <= step)
        {
            transform.localPosition = targetLocal;
            AdvanceTarget();

            if (waitTime > 0f)
            {
                isWaiting = true;
                waitTimer = waitTime;
            }
        }
        else
        {
            transform.localPosition = (Vector3)(currentLocal +
                (targetLocal - currentLocal).normalized * step);
        }
    }

    bool IsBlocked(Vector2 moveDir, float step)
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return false;

        Vector2 size = new Vector2(
            Mathf.Max(0.01f, col.bounds.size.x - collisionShrink),
            Mathf.Max(0.01f, col.bounds.size.y - collisionShrink));

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            col.bounds.center,
            size,
            0f,
            moveDir,
            step + 0.01f,
            LayerMask.GetMask("Platform", "Blocker")
        );

        foreach (var hit in hits)
        {
            if (hit.collider.transform == transform) continue;
            // 같은 MapPiece 소속은 무시
            if (transform.parent != null &&
                hit.collider.transform.IsChildOf(transform.parent)) continue;

            float dot = Vector2.Dot(moveDir, hit.normal);
            if (dot < -0.5f) return true;
        }

        return false;
    }

    void ReverseDirection()
    {
        if (mode == MovingPlatformMode.PingPong)
        {
            direction = -direction;
            currentTarget = Mathf.Clamp(currentTarget + direction, 0, localPoints.Count - 1);
        }
        else
        {
            // Loop 모드는 경로 역순
            localPoints.Reverse();
            currentTarget = localPoints.Count - 1 - currentTarget;
        }
    }

    void AdvanceTarget()
    {
        if (mode == MovingPlatformMode.PingPong)
        {
            if (currentTarget == localPoints.Count - 1) direction = -1;
            else if (currentTarget == 0) direction = 1;
            currentTarget += direction;
        }
        else
        {
            currentTarget = (currentTarget + 1) % localPoints.Count;
        }
    }

    public void Initialize(MovingPlatformData data)
    {
        mode = data.mode;
        speed = data.speed;
        waitTime = data.waitTime;
        pointA = data.pointA.ToVector2();
        pointB = data.pointB.ToVector2();

        loopPoints.Clear();
        foreach (var lp in data.loopPoints)
            loopPoints.Add(lp.ToVector2());

        transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector2 initLocal = Application.isPlaying
            ? initialLocalPosition
            : (Vector2)(Vector3)transform.localPosition;

        Vector2 parentPos = transform.parent != null
            ? (Vector2)transform.parent.position
            : Vector2.zero;

        if (mode == MovingPlatformMode.PingPong)
        {
            Vector2 wA = parentPos + initLocal + pointA;
            Vector2 wB = parentPos + initLocal + pointB;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(wA, 0.15f);
            Gizmos.DrawWireSphere(wB, 0.15f);
            Gizmos.DrawLine(wA, wB);
        }
        else
        {
            var pts = new List<Vector2> { parentPos + initLocal + pointA };
            foreach (var lp in loopPoints)
                pts.Add(parentPos + initLocal + lp);

            Gizmos.color = Color.cyan;
            for (int i = 0; i < pts.Count; i++)
            {
                Gizmos.DrawWireSphere(pts[i], 0.15f);
                Gizmos.DrawLine(pts[i], pts[(i + 1) % pts.Count]);
            }
        }
    }
#endif
}