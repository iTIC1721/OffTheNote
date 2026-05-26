using UnityEngine;

/// <summary>
/// 스프라이트 가장자리에 펜으로 그린 듯한 노이즈 윤곽선을 그리는 컴포넌트.
/// MeshRenderer로 윤곽선 메시를 직접 생성해 노이즈를 적용.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class OutlineObject : MonoBehaviour
{
    [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = new Color(0.15f, 0.08f, 0.03f, 1f);
    [SerializeField] private float outlineWidth = 0.06f;

    [Header("Pen Noise")]
    [SerializeField] private float noiseAmount = 0.015f;
    [SerializeField] private float noiseFrequency = 8f;
    [SerializeField] private int segments = 40;
    [SerializeField] private float lineThickness = 0.02f;

    [Header("Interior Strokes")]
    [SerializeField] private bool drawStrokes = true;
    [SerializeField] private float strokeSpacing = 0.3f;   // 선 간격
    [SerializeField] private float strokeAngle = 60f;    // 선 각도 (도)
    [SerializeField] private float strokeThickness = 0.012f; // 선 두께
    [SerializeField] private float strokeNoiseAmount = 0.02f; // 선 노이즈 강도
    [SerializeField] private int strokeSegments = 8;      // 선 분할 수

    [Header("Animation")]
    [SerializeField] private bool animate = false;
    [SerializeField] private float animSpeed = 0.8f;

    private GameObject outlineObj;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private SpriteRenderer mainSr;
    private Mesh outlineMesh;
    private float timeOffset;

    // 단색 머티리얼 캐시
    private Material outlineMaterial;

    void OnEnable()
    {
        mainSr = GetComponent<SpriteRenderer>();
        timeOffset = Random.Range(0f, 100f); // 오브젝트마다 다른 노이즈 패턴
        CreateOutlineObject();
        RebuildMesh();
    }

    void OnDisable()
    {
        DestroyOutlineObject();
    }

    void OnDestroy()
    {
        DestroyOutlineObject();
        if (outlineMesh != null) DestroyImmediate(outlineMesh);
        if (outlineMaterial != null) DestroyImmediate(outlineMaterial);
    }

    void CreateOutlineObject()
    {
        DestroyOutlineObject();

        outlineObj = new GameObject("_PenOutline");
        outlineObj.hideFlags = HideFlags.HideAndDontSave;
        outlineObj.transform.SetParent(transform, false);
        outlineObj.transform.localPosition = new Vector3(0, 0, 0.01f);

        // 부모 scale 상속을 상쇄
        Vector3 s = transform.lossyScale;
        outlineObj.transform.localScale = new Vector3(
            Mathf.Abs(s.x) > 0.0001f ? 1f / s.x : 1f,
            Mathf.Abs(s.y) > 0.0001f ? 1f / s.y : 1f,
            1f);

        meshFilter = outlineObj.AddComponent<MeshFilter>();
        meshRenderer = outlineObj.AddComponent<MeshRenderer>();

        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = outlineColor;
        meshRenderer.sharedMaterial = outlineMaterial;

        meshRenderer.sortingLayerID = mainSr.sortingLayerID;
        meshRenderer.sortingOrder = mainSr.sortingOrder + 1;

        outlineMesh = new Mesh();
        outlineMesh.name = "OutlineMesh";
        meshFilter.mesh = outlineMesh;
    }

    void DestroyOutlineObject()
    {
        if (outlineObj != null)
        {
            DestroyImmediate(outlineObj);
            outlineObj = null;
        }
    }

    void Update()
    {
        if (mainSr == null || outlineObj == null) return;

        // 부모 scale 변경 시 자식 scale 동기화
        Vector3 s = transform.lossyScale;
        outlineObj.transform.localScale = new Vector3(
            Mathf.Abs(s.x) > 0.0001f ? 1f / s.x : 1f,
            Mathf.Abs(s.y) > 0.0001f ? 1f / s.y : 1f,
            1f);

        // Sorting 동기화
        meshRenderer.sortingLayerID = mainSr.sortingLayerID;
        meshRenderer.sortingOrder = mainSr.sortingOrder + 1;

        if (animate || Application.isEditor)
            RebuildMesh();
    }

    void RebuildMesh()
    {
        if (outlineMesh == null || mainSr == null || mainSr.sprite == null) return;

        // 스프라이트 로컬 크기 × localScale = 실제 로컬 공간 크기
        Vector2 spriteSize = mainSr.sprite.bounds.size;
        Vector2 spriteCenter = mainSr.sprite.bounds.center;
        Vector3 ls = transform.localScale;

        float w = spriteSize.x * Mathf.Abs(ls.x) * 0.5f + outlineWidth;
        float h = spriteSize.y * Mathf.Abs(ls.y) * 0.5f + outlineWidth;
        Vector2 center = new Vector2(
            spriteCenter.x * ls.x,
            spriteCenter.y * ls.y);

        // 사각형의 네 꼭짓점 (시계 방향)
        Vector2[] corners = new Vector2[]
        {
        new Vector2(center.x - w, center.y - h),
        new Vector2(center.x - w, center.y + h),
        new Vector2(center.x + w, center.y + h),
        new Vector2(center.x + w, center.y - h),
        };

        float t = animate ? (Time.time * animSpeed + timeOffset) : timeOffset;

        var points = new System.Collections.Generic.List<Vector2>();

        for (int side = 0; side < 4; side++)
        {
            Vector2 from = corners[side];
            Vector2 to = corners[(side + 1) % 4];

            for (int seg = 0; seg < segments; seg++)
            {
                float ratio = (float)seg / segments;
                Vector2 pos = Vector2.Lerp(from, to, ratio);
                Vector2 dir = (to - from).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float noiseVal = PerlinNoise1D(ratio * noiseFrequency + side * 10f + t) * 2f - 1f;
                pos += perp * noiseVal * noiseAmount;
                points.Add(pos);
            }
        }

        BuildLineMesh(points, lineThickness);

        // AddStrokesToMesh도 스프라이트 로컬 크기 기준으로 전달
        if (drawStrokes)
        {
            // Bounds를 로컬 기준으로 재구성
            Bounds localBounds = new Bounds(spriteCenter, spriteSize);
            AddStrokesToMesh(localBounds, w, h, center);
        }

        if (outlineMaterial != null)
            outlineMaterial.color = outlineColor;
    }

    void AddStrokesToMesh(Bounds b, float w, float h, Vector2 center)
    {
        var allVerts = new System.Collections.Generic.List<Vector3>(outlineMesh.vertices);
        var allTris = new System.Collections.Generic.List<int>(outlineMesh.triangles);
        var allUvs = new System.Collections.Generic.List<Vector2>(outlineMesh.uv);

        float t = animate ? (Time.time * animSpeed * 0.3f + timeOffset) : timeOffset;
        float rad = strokeAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 perp = new Vector2(-dir.y, dir.x); // 선에 수직 방향 (간격 방향)

        // 스프라이트 대각선 길이 기준으로 선 개수 결정
        float diagonal = Mathf.Sqrt(w * w + h * h) * 2f;
        int lineCount = Mathf.CeilToInt(diagonal / strokeSpacing);

        for (int i = 0; i < lineCount; i++)
        {
            // perp 방향으로 strokeSpacing 간격으로 중심선 이동
            float offset = (i - lineCount * 0.5f) * strokeSpacing;
            Vector2 lineMid = center + perp * offset;

            // 선의 시작/끝 점을 스프라이트 바운드로 클리핑
            Vector2 lineStart, lineEnd;
            if (!ClipLineToRect(lineMid, dir, center, w, h, out lineStart, out lineEnd))
                continue;

            // 선을 strokeSegments로 분할해 노이즈 적용
            float lineLen = Vector2.Distance(lineStart, lineEnd);
            var strokePts = new System.Collections.Generic.List<Vector2>();

            for (int s = 0; s <= strokeSegments; s++)
            {
                float ratio = (float)s / strokeSegments;
                Vector2 pos = Vector2.Lerp(lineStart, lineEnd, ratio);

                // 수직 방향 노이즈
                float noiseVal = PerlinNoise1D(ratio * 6f + i * 3.7f + t) * 2f - 1f;
                pos += perp * noiseVal * strokeNoiseAmount;

                strokePts.Add(pos);
            }

            // 메시 병합
            int baseVert = allVerts.Count;
            int count = strokePts.Count;

            for (int s = 0; s < count; s++)
            {
                Vector2 curr = strokePts[s];
                Vector2 next = strokePts[Mathf.Min(s + 1, count - 1)];
                Vector2 d = next - curr;
                if (d.magnitude < 0.0001f) d = dir;
                Vector2 pn = new Vector2(-d.normalized.y, d.normalized.x) * strokeThickness * 0.5f;

                allVerts.Add(curr + pn);
                allVerts.Add(curr - pn);
                allUvs.Add(new Vector2(0, (float)s / count));
                allUvs.Add(new Vector2(1, (float)s / count));
            }

            for (int s = 0; s < count - 1; s++)
            {
                int vi = baseVert + s * 2;
                int ni = vi + 2;
                allTris.Add(vi); allTris.Add(ni); allTris.Add(vi + 1);
                allTris.Add(ni); allTris.Add(ni + 1); allTris.Add(vi + 1);
            }
        }

        outlineMesh.Clear();
        outlineMesh.vertices = allVerts.ToArray();
        outlineMesh.triangles = allTris.ToArray();
        outlineMesh.uv = allUvs.ToArray();
        outlineMesh.RecalculateNormals();
    }

    // 직선을 사각형 영역으로 클리핑 — 교차점을 시작/끝으로 반환
    bool ClipLineToRect(Vector2 mid, Vector2 dir, Vector2 center, float hw, float hh,
                        out Vector2 start, out Vector2 end)
    {
        start = end = mid;
        float tMin = float.MinValue;
        float tMax = float.MaxValue;

        float[] deltas = { dir.x, -dir.x, dir.y, -dir.y };
        float[] dists = {
            center.x + hw - mid.x,
            mid.x - (center.x - hw),
            center.y + hh - mid.y,
            mid.y - (center.y - hh)
        };

        for (int k = 0; k < 4; k++)
        {
            if (Mathf.Abs(deltas[k]) < 0.0001f)
            {
                if (dists[k] < 0) return false;
            }
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

    // 포인트 목록으로 폐곡선 메시 생성
    void BuildLineMesh(System.Collections.Generic.List<Vector2> points, float thickness)
    {
        int count = points.Count;
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[count * 6];
        Vector2[] uvs = new Vector2[count * 2];

        for (int i = 0; i < count; i++)
        {
            Vector2 curr = points[i];
            Vector2 next = points[(i + 1) % count];
            Vector2 dir = (next - curr).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

            vertices[i * 2] = curr + perp;
            vertices[i * 2 + 1] = curr - perp;

            uvs[i * 2] = new Vector2(0, (float)i / count);
            uvs[i * 2 + 1] = new Vector2(1, (float)i / count);

            int ti = i * 6;
            int vi = i * 2;
            int ni = ((i + 1) % count) * 2;

            triangles[ti] = vi;
            triangles[ti + 1] = ni;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = ni;
            triangles[ti + 4] = ni + 1;
            triangles[ti + 5] = vi + 1;
        }

        outlineMesh.Clear();
        outlineMesh.vertices = vertices;
        outlineMesh.triangles = triangles;
        outlineMesh.uv = uvs;
        outlineMesh.RecalculateNormals();
    }

    // 1D Perlin 노이즈 (Unity의 Mathf.PerlinNoise 활용)
    float PerlinNoise1D(float x)
    {
        return Mathf.PerlinNoise(x, x * 0.3f);
    }

    void OnValidate()
    {
        segments = Mathf.Max(4, segments);
        noiseAmount = Mathf.Max(0, noiseAmount);
        lineThickness = Mathf.Max(0.001f, lineThickness);

        if (outlineObj != null)
            RebuildMesh();
    }
}