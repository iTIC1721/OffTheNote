using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이저 발사 장치.
/// 지정된 방향으로 레이저를 발사하며, 플랫폼/블로커에 닿으면 멈춤.
/// 발사 장치 자체는 Platform 레이어로 동작.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LaserEmitter : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private LaserDirection direction = LaserDirection.Right;
    [SerializeField] private float maxLength = 20f;
    [SerializeField] private float hitWidth = 0.03f;
    [SerializeField] private float laserWidth = 0.08f;
    [SerializeField] private Color laserColor = new Color(0.9f, 0.2f, 0.1f, 1f);

    [Header("Pen Noise")]
    [SerializeField] private float noiseAmount = 0.02f;
    [SerializeField] private float noiseFrequency = 6f;
    [SerializeField] private int segments = 30;
    [SerializeField] private bool animate = true;
    [SerializeField] private float animSpeed = 1.2f;

    [Header("Hazard")]
    [SerializeField] private float hazardMargin = 0.05f;

    // ── 내부 오브젝트 ──────────────────────────────────────────
    private GameObject laserGO;
    private MeshFilter laserFilter;
    private MeshRenderer laserRenderer;
    private Mesh laserMesh;
    private Material laserMat;

    private PlayerController player;
    private BoxCollider2D playerCol;

    private float timeOffset;
    private float currentLength;

    void OnEnable()
    {
        timeOffset = Random.Range(0f, 100f);
        CreateLaserObject();
        player = FindFirstObjectByType<PlayerController>();
        playerCol = player?.GetComponent<BoxCollider2D>();
    }

    void OnDisable() => DestroyLaserObject();
    void OnDestroy() => DestroyLaserObject();

    void CreateLaserObject()
    {
        DestroyLaserObject();

        laserGO = new GameObject("_Laser");
        laserGO.transform.SetParent(transform, false);
        laserGO.transform.localPosition = Vector3.zero;

        laserFilter = laserGO.AddComponent<MeshFilter>();
        laserRenderer = laserGO.AddComponent<MeshRenderer>();
        laserMesh = new Mesh { name = "LaserMesh" };
        laserFilter.mesh = laserMesh;
        laserMat = new Material(Shader.Find("Sprites/Default"));
        laserMat.color = laserColor;
        laserRenderer.sharedMaterial = laserMat;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            laserRenderer.sortingLayerID = sr.sortingLayerID;
            laserRenderer.sortingOrder = sr.sortingOrder + 1;
        }
    }

    void DestroyLaserObject()
    {
        if (laserGO != null) { DestroyImmediate(laserGO); laserGO = null; }
        if (laserMesh != null) { DestroyImmediate(laserMesh); laserMesh = null; }
        if (laserMat != null) { DestroyImmediate(laserMat); laserMat = null; }
    }

    void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        UpdateLaserLength();
        BuildLaserMesh();
        CheckHazard();
    }

    // ── 레이저 길이 계산 ──────────────────────────────────────
    void UpdateLaserLength()
    {
        Vector2 origin = transform.position;
        Vector2 dir = GetDirectionVector();

        RaycastHit2D hit = Physics2D.Raycast(
            origin + dir * 0.6f, // 발사 장치 자체를 건너뜀
            dir,
            maxLength,
            LayerMask.GetMask("Platform"));

        currentLength = hit.collider != null
            ? hit.distance
            : maxLength;
    }

    // ── 레이저 메시 생성 (펄린 노이즈 적용) ──────────────────
    void BuildLaserMesh()
    {
        if (laserMesh == null) return;

        Vector2 dir = GetDirectionVector();
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float t = animate ? (Time.time * animSpeed + timeOffset) : timeOffset;

        // 발사 장치 중심에서 시작 (0.5f = 장치 반지름)
        Vector2 startLocal = dir * 0.5f;
        Vector2 endLocal = dir * (0.5f + currentLength);

        var pts = new List<Vector2>();
        for (int i = 0; i <= segments; i++)
        {
            float ratio = (float)i / segments;
            Vector2 pos = Vector2.Lerp(startLocal, endLocal, ratio);

            // 양 끝에서 노이즈 페이드 (끝부분 자연스럽게)
            float fade = Mathf.Sin(ratio * Mathf.PI);
            float noise = (Mathf.PerlinNoise(ratio * noiseFrequency + t, t * 0.3f) * 2f - 1f)
                            * noiseAmount * fade;
            pos += perp * noise;
            pts.Add(pos);
        }

        BuildLineMesh(pts, laserWidth);
        laserMat.color = laserColor;
    }

    void BuildLineMesh(List<Vector2> pts, float thickness)
    {
        int count = pts.Count;
        if (count < 2) return;

        var verts = new Vector3[count * 2];
        var tris = new List<int>();
        var uvs = new Vector2[count * 2];

        for (int i = 0; i < count; i++)
        {
            Vector2 curr = pts[i];
            Vector2 next = pts[Mathf.Min(i + 1, count - 1)];
            Vector2 d = (next - curr);
            if (d.magnitude < 0.0001f && i > 0) d = pts[i] - pts[i - 1];
            Vector2 perp = new Vector2(-d.normalized.y, d.normalized.x);

            // 끝으로 갈수록 가늘어지는 효과
            float taper = 1f - (float)i / count * 0.5f;
            Vector2 pn = perp * (thickness * 0.5f * taper);

            verts[i * 2] = curr + pn;
            verts[i * 2 + 1] = curr - pn;
            uvs[i * 2] = new Vector2(0, (float)i / count);
            uvs[i * 2 + 1] = new Vector2(1, (float)i / count);
        }

        for (int i = 0; i < count - 1; i++)
        {
            int vi = i * 2, ni = (i + 1) * 2;
            tris.Add(vi); tris.Add(ni); tris.Add(vi + 1);
            tris.Add(ni); tris.Add(ni + 1); tris.Add(vi + 1);
        }

        laserMesh.Clear();
        laserMesh.vertices = verts;
        laserMesh.triangles = tris.ToArray();
        laserMesh.uv = uvs;
        laserMesh.RecalculateNormals();
    }

    // ── 플레이어 충돌 감지 ────────────────────────────────────
    void CheckHazard()
    {
        if (player == null || playerCol == null) return;

        Vector2 dir = GetDirectionVector();
        Vector2 origin = (Vector2)transform.position + dir * 0.5f;
        Vector2 end = origin + dir * currentLength;

        // 레이저를 선분으로 근사 — 플레이어 콜라이더와 겹침 검사
        Vector3 scale = player.transform.lossyScale;
        Vector2 worldSize = new Vector2(
            playerCol.size.x * Mathf.Abs(scale.x) - hazardMargin * 2f,
            playerCol.size.y * Mathf.Abs(scale.y) - hazardMargin * 2f);
        worldSize = Vector2.Max(worldSize, Vector2.one * 0.01f);

        Vector2 playerPos = player.transform.position;

        // 플레이어 박스와 레이저 선분의 교차 여부 검사
        if (IsLineIntersectingBox(origin, end, playerPos, worldSize))
            player.Respawn();
    }

    bool IsLineIntersectingBox(Vector2 lineStart, Vector2 lineEnd,
                                Vector2 boxCenter, Vector2 boxSize)
    {
        // 레이저 방향 BoxCast로 플레이어 감지
        Vector2 dir = lineEnd - lineStart;
        float dist = dir.magnitude;
        if (dist < 0.001f) return false;

        // 레이저 두께를 고려한 BoxCast
        Vector2 castSize = new Vector2(hitWidth, hitWidth);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            lineStart, castSize, 0f, dir.normalized, dist,
            LayerMask.GetMask("Player"));

        return hits.Length > 0;
    }

    Vector2 GetDirectionVector()
    {
        // 초기 방향을 transform 회전에 적용
        Vector2 baseDir = direction switch
        {
            LaserDirection.Right => Vector2.right,
            LaserDirection.Left => Vector2.left,
            LaserDirection.Up => Vector2.up,
            LaserDirection.Down => Vector2.down,
            _ => Vector2.right,
        };

        // 발사 장치의 회전을 반영
        return transform.TransformDirection(baseDir);
    }

    public void Initialize(LaserEmitterData data)
    {
        direction = data.direction;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector2 dir = GetDirectionVector();
        Vector2 origin = (Vector2)transform.position + dir * 0.5f;
        Gizmos.color = new Color(0.9f, 0.2f, 0.1f, 0.5f);
        Gizmos.DrawRay(origin, dir * maxLength);
    }
#endif
}

public enum LaserDirection { Right, Left, Up, Down }