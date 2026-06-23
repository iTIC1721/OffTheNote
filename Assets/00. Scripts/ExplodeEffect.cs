using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 사망 시 OutlineObject와 동일한 펜 노이즈 메시 스타일로
/// 앙증맞게 터지는 이펙트를 재생합니다.
///
/// 코루틴은 이펙트 전용 임시 GameObject(EffectRunner)에서 실행되므로
/// Destroy 한 번으로 코루틴도 같이 소멸됩니다.
/// 연속 사망 시 이전 이펙트를 중단하지 않고 새 이펙트와 공존합니다.
/// </summary>
public class ExplodeEffect : MonoBehaviour
{
    [Header("Burst Pieces")]
    [SerializeField] private int pieceCount = 8;            // 터지는 조각 수
    [SerializeField] private float burstSpeed = 2.5f;       // 초기 날아가는 속도
    [SerializeField] private float burstSpeedVariance = 1f; // 속도 분산
    [SerializeField] private float gravity = -6f;           // 조각에 적용될 중력
    [SerializeField] private float duration = 0.7f;         // 이펙트 지속 시간
    [SerializeField] private float fadeStartRatio = 0.5f;   // duration의 몇 % 시점부터 페이드

    [Header("Piece Shape")]
    [SerializeField] private float pieceSize = 0.12f;       // 조각 크기
    [SerializeField] private float pieceSizeVariance = 0.06f;
    [SerializeField] private int pieceSegments = 14;        // 조각 외곽선 분할 수
    [SerializeField] private float lineThickness = 0.022f;
    [SerializeField] private float noiseAmount = 0.018f;
    [SerializeField] private float noiseFrequency = 7f;

    [Header("Strokes (해칭)")]
    [SerializeField] private bool drawStrokes = true;
    [SerializeField] private float strokeSpacing = 0.09f;
    [SerializeField] private float strokeThickness = 0.01f;
    [SerializeField] private float strokeNoiseAmount = 0.015f;
    [SerializeField] private int strokeSegments = 5;

    [Header("Visual")]
    [SerializeField] private Color pieceColor = new Color(0.15f, 0.08f, 0.03f, 1f);
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    // 현재 재생 중인 이펙트 루트 목록 (복수 공존 가능)
    private readonly List<GameObject> activeRoots = new List<GameObject>();

    // ── 외부 호출용 ─────────────────────────────────────────────

    /// <summary>현재 위치에서 이펙트 재생</summary>
    public void Play()
    {
        Play(transform.position);
    }

    /// <summary>지정 위치에서 이펙트 재생 (이전 이펙트와 공존)</summary>
    public void Play(Vector2 position)
    {
        // 이펙트 전용 루트 생성
        var root = new GameObject("_DeathEffect");
        root.transform.position = new Vector3(position.x, position.y, -1f);
        activeRoots.Add(root);

        // 코루틴을 root에 붙은 EffectRunner에서 실행
        // → Destroy(root) 시 MonoBehaviour도 같이 소멸되어 코루틴이 자동 중단됨
        var runner = root.AddComponent<EffectRunner>();
        runner.Run(RunEffect(root, this));
    }

    // ── 코루틴 ──────────────────────────────────────────────────

    // owner: 설정값을 읽기 위한 참조 (root가 살아있는 동안만 실행됨)
    private static IEnumerator RunEffect(GameObject root, ExplodeEffect owner)
    {
        var pieces = new PieceState[owner.pieceCount];
        for (int i = 0; i < owner.pieceCount; i++)
            pieces[i] = owner.CreatePiece(root, i);

        float elapsed = 0f;

        while (elapsed < owner.duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / owner.duration;
            float alpha = t < owner.fadeStartRatio
                ? 1f
                : 1f - (t - owner.fadeStartRatio) / (1f - owner.fadeStartRatio);
            alpha = Mathf.Clamp01(alpha);

            foreach (var p in pieces)
                owner.UpdatePiece(p, Time.deltaTime, alpha);

            yield return null;
        }

        // 정상 종료: root를 파괴하고 목록에서 제거
        if (owner != null)
            owner.activeRoots.Remove(root);
        Destroy(root);
    }

    // ── 조각 생성 ───────────────────────────────────────────────

    private PieceState CreatePiece(GameObject root, int index)
    {
        float angle = (360f / pieceCount) * index + Random.Range(-20f, 20f);
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        float speed = burstSpeed + Random.Range(-burstSpeedVariance, burstSpeedVariance);
        float size = pieceSize + Random.Range(-pieceSizeVariance, pieceSizeVariance);
        size = Mathf.Max(0.04f, size);
        float rotSpeed = Random.Range(-180f, 180f); // 회전 속도 (도/초)
        float noiseOffset = Random.Range(0f, 100f);

        // 조각 모양 선택 (0=닫힌 외곽선, 1=작은 X, 2=별모양 찌그러진 원)
        int shapeType = Random.Range(0, 3);

        var go = new GameObject($"_Piece_{index}");
        go.transform.SetParent(root.transform, false);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = pieceColor;
        mr.material = mat;
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = sortingOrder;

        var mesh = new Mesh();
        mesh.name = "PieceMesh";
        mf.mesh = mesh;

        var state = new PieceState
        {
            go = go,
            mr = mr,
            mat = mat,
            mesh = mesh,
            velocity = dir * speed,
            rotSpeed = rotSpeed,
            rotation = Random.Range(0f, 360f),
            size = size,
            noiseOffset = noiseOffset,
            shapeType = shapeType,
        };

        BuildPieceMesh(state);
        return state;
    }

    private void UpdatePiece(PieceState p, float dt, float alpha)
    {
        if (p.go == null) return; // 오브젝트가 이미 파괴된 경우 방어

        // 물리
        p.velocity.y += gravity * dt;
        p.go.transform.localPosition += (Vector3)(p.velocity * dt);
        p.rotation += p.rotSpeed * dt;
        p.go.transform.localRotation = Quaternion.Euler(0, 0, p.rotation);

        // 알파 갱신
        Color c = pieceColor;
        c.a = alpha;
        p.mat.color = c;
    }

    // ── 메시 빌드 ────────────────────────────────────────────────

    private void BuildPieceMesh(PieceState p)
    {
        var allVerts = new List<Vector3>();
        var allTris = new List<int>();
        var allUvs = new List<Vector2>();

        switch (p.shapeType)
        {
            case 0: BuildRoundedSquare(p, allVerts, allTris, allUvs); break;
            case 1: BuildCrossShape(p, allVerts, allTris, allUvs); break;
            default: BuildWobblyCircle(p, allVerts, allTris, allUvs); break;
        }

        if (drawStrokes)
            AddHatching(p, allVerts, allTris, allUvs);

        p.mesh.Clear();
        p.mesh.vertices = allVerts.ToArray();
        p.mesh.triangles = allTris.ToArray();
        p.mesh.uv = allUvs.ToArray();
        p.mesh.RecalculateNormals();
    }

    // 찌그러진 사각형 외곽선
    private void BuildRoundedSquare(PieceState p,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        float hw = p.size * 0.5f;
        float hh = p.size * 0.45f;
        float t = p.noiseOffset;

        Vector2[] corners = {
            new Vector2(-hw, -hh),
            new Vector2(-hw,  hh),
            new Vector2( hw,  hh),
            new Vector2( hw, -hh),
        };

        var points = new List<Vector2>();
        int segsPerSide = pieceSegments / 4 + 1;

        for (int side = 0; side < 4; side++)
        {
            Vector2 from = corners[side];
            Vector2 to = corners[(side + 1) % 4];
            for (int s = 0; s < segsPerSide; s++)
            {
                float ratio = (float)s / segsPerSide;
                Vector2 pos = Vector2.Lerp(from, to, ratio);
                Vector2 dir = (to - from).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float nv = PerlinNoise1D(ratio * noiseFrequency + side * 9.3f + t) * 2f - 1f;
                pos += perp * nv * noiseAmount;
                points.Add(pos);
            }
        }

        AppendLineLoop(points, lineThickness, verts, tris, uvs);
    }

    // X (십자) 모양
    private void BuildCrossShape(PieceState p,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        float r = p.size * 0.55f;
        float t = p.noiseOffset;

        // 두 선: 대각선 방향
        Vector2[][] lines = {
            new[] { new Vector2(-r, -r), new Vector2( r,  r) },
            new[] { new Vector2(-r,  r), new Vector2( r, -r) },
        };

        foreach (var line in lines)
        {
            var pts = new List<Vector2>();
            for (int s = 0; s <= pieceSegments; s++)
            {
                float ratio = (float)s / pieceSegments;
                Vector2 pos = Vector2.Lerp(line[0], line[1], ratio);
                Vector2 dir = (line[1] - line[0]).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float nv = PerlinNoise1D(ratio * noiseFrequency + t + s * 0.3f) * 2f - 1f;
                pos += perp * nv * noiseAmount;
                pts.Add(pos);
            }
            AppendLineStrip(pts, lineThickness, verts, tris, uvs);
        }
    }

    // 찌그러진 원
    private void BuildWobblyCircle(PieceState p,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        float r = p.size * 0.5f;
        float t = p.noiseOffset;
        var points = new List<Vector2>();

        for (int i = 0; i < pieceSegments; i++)
        {
            float ratio = (float)i / pieceSegments;
            float angle = ratio * Mathf.PI * 2f;
            float nv = PerlinNoise1D(ratio * noiseFrequency + t) * 2f - 1f;
            float rad = r + nv * noiseAmount * 3f;
            points.Add(new Vector2(Mathf.Cos(angle) * rad, Mathf.Sin(angle) * rad));
        }

        AppendLineLoop(points, lineThickness, verts, tris, uvs);
    }

    // 해칭 선 추가
    private void AddHatching(PieceState p,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        float hw = p.size * 0.5f;
        float hh = p.size * 0.5f;
        float t = p.noiseOffset * 0.4f;

        float rad = 45f * Mathf.Deg2Rad; // 고정 각도
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 perp = new Vector2(-dir.y, dir.x);

        float diagonal = Mathf.Sqrt(hw * hw + hh * hh) * 2f;
        int lineCount = Mathf.CeilToInt(diagonal / strokeSpacing);

        for (int i = 0; i < lineCount; i++)
        {
            float offset = (i - lineCount * 0.5f) * strokeSpacing;
            Vector2 mid = perp * offset;

            Vector2 lineStart, lineEnd;
            if (!ClipLineToRect(mid, dir, Vector2.zero, hw, hh, out lineStart, out lineEnd))
                continue;

            var pts = new List<Vector2>();
            for (int s = 0; s <= strokeSegments; s++)
            {
                float ratio = (float)s / strokeSegments;
                Vector2 pos = Vector2.Lerp(lineStart, lineEnd, ratio);
                float nv = PerlinNoise1D(ratio * 5f + i * 3.1f + t) * 2f - 1f;
                pos += perp * nv * strokeNoiseAmount;
                pts.Add(pos);
            }

            AppendLineStrip(pts, strokeThickness, verts, tris, uvs);
        }
    }

    // ── 메시 유틸 ────────────────────────────────────────────────

    // 폐곡선 (첫점-끝점 연결)
    private void AppendLineLoop(List<Vector2> pts, float thickness,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        int count = pts.Count;
        int baseV = verts.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 curr = pts[i];
            Vector2 next = pts[(i + 1) % count];
            Vector2 d = (next - curr);
            if (d.magnitude < 0.0001f) d = Vector2.right;
            Vector2 perp = new Vector2(-d.normalized.y, d.normalized.x) * thickness * 0.5f;
            verts.Add(curr + perp);
            verts.Add(curr - perp);
            uvs.Add(new Vector2(0, (float)i / count));
            uvs.Add(new Vector2(1, (float)i / count));
        }

        for (int i = 0; i < count; i++)
        {
            int vi = baseV + i * 2;
            int ni = baseV + ((i + 1) % count) * 2;
            tris.Add(vi); tris.Add(ni); tris.Add(vi + 1);
            tris.Add(ni); tris.Add(ni + 1); tris.Add(vi + 1);
        }
    }

    // 열린 선
    private void AppendLineStrip(List<Vector2> pts, float thickness,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        int count = pts.Count;
        int baseV = verts.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 curr = pts[i];
            Vector2 next = pts[Mathf.Min(i + 1, count - 1)];
            Vector2 d = next - curr;
            if (d.magnitude < 0.0001f) d = Vector2.right;
            Vector2 perp = new Vector2(-d.normalized.y, d.normalized.x) * thickness * 0.5f;
            verts.Add(curr + perp);
            verts.Add(curr - perp);
            uvs.Add(new Vector2(0, (float)i / count));
            uvs.Add(new Vector2(1, (float)i / count));
        }

        for (int i = 0; i < count - 1; i++)
        {
            int vi = baseV + i * 2;
            int ni = vi + 2;
            tris.Add(vi); tris.Add(ni); tris.Add(vi + 1);
            tris.Add(ni); tris.Add(ni + 1); tris.Add(vi + 1);
        }
    }

    private bool ClipLineToRect(Vector2 mid, Vector2 dir, Vector2 center,
        float hw, float hh, out Vector2 start, out Vector2 end)
    {
        start = end = mid;
        float tMin = float.MinValue, tMax = float.MaxValue;

        float[] deltas = { dir.x, -dir.x, dir.y, -dir.y };
        float[] dists = {
            center.x + hw - mid.x,
            mid.x - (center.x - hw),
            center.y + hh - mid.y,
            mid.y - (center.y - hh)
        };

        for (int k = 0; k < 4; k++)
        {
            if (Mathf.Abs(deltas[k]) < 0.0001f) { if (dists[k] < 0) return false; }
            else
            {
                float tt = dists[k] / deltas[k];
                if (deltas[k] > 0) tMax = Mathf.Min(tMax, tt);
                else tMin = Mathf.Max(tMin, tt);
            }
        }

        if (tMin > tMax) return false;
        start = mid + dir * tMin;
        end = mid + dir * tMax;
        return true;
    }

    private float PerlinNoise1D(float x) => Mathf.PerlinNoise(x, x * 0.3f);

    // ── 경량 코루틴 러너 ─────────────────────────────────────────

    /// <summary>
    /// 이펙트 root GameObject에 붙어서 코루틴을 소유합니다.
    /// root가 Destroy되면 이 컴포넌트도 소멸되어 코루틴이 자동 중단됩니다.
    /// </summary>
    private class EffectRunner : MonoBehaviour
    {
        public void Run(IEnumerator routine) => StartCoroutine(routine);
    }

    // ── 내부 데이터 구조 ─────────────────────────────────────────

    private class PieceState
    {
        public GameObject go;
        public MeshRenderer mr;
        public Material mat;
        public Mesh mesh;
        public Vector2 velocity;
        public float rotSpeed;
        public float rotation;
        public float size;
        public float noiseOffset;
        public int shapeType;   // 0=사각, 1=X, 2=원
    }
}