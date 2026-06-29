using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class StageEditorWindow : EditorWindow
{
    // ── 레이아웃 ──────────────────────────────────────────────
    private float panelWidth = 420f;          // 7. 너비 고정값 증가
    private const float PANEL_MIN = 320f;
    private const float PANEL_MAX = 600f;
    private bool isResizingPanel = false;
    private bool isMinimized = false;
    private const float MINIMIZED_HEIGHT = 28f;

    // ── 상태 ──────────────────────────────────────────────────
    private StageData stageData = new StageData();
    private string savePath = "Assets/StreamingAssets/stage_01.json";
    private string testScenePath = "Assets/01. Scenes/Tools/TestScene.unity";
    private Vector2 scrollPos;
    private int selectedPieceIndex = -1;
    private int selectedPlatformIndex = -1;

    // ── Undo/Redo ─────────────────────────────────────────────
    private List<string> undoStack = new List<string>();
    private List<string> redoStack = new List<string>();
    private const int MAX_UNDO = 50;

    // ── 복사 버퍼 ─────────────────────────────────────────────
    private MapPieceData copiedPiece = null;
    private PieceObjectData copiedPieceObject = null;

    // ── 저장 경로 추적 ────────────────────────────────────────
    private string lastSavedPath = "";

    // ── 스냅 ──────────────────────────────────────────────────
    private float snapPosition = 0.5f;
    private float snapSize = 0.5f;

    // ── 뷰포트 ────────────────────────────────────────────────
    private Vector2 viewOffset = Vector2.zero;
    private float viewScale = 40f;
    private bool isPanning = false;
    private Vector2 panStart;
    private Vector2 panOffsetStart;

    private enum DragTarget { None, Piece, Platform, Spawn, Goal }
    private DragTarget dragTarget = DragTarget.None;
    private int dragPieceIndex = -1;
    private int dragPlatformIndex = -1;
    private Vector2 dragStartWorld;
    private Vector2 dragStartObjPos;
    private bool didDrag = false;

    [MenuItem("Tools/Stage Editor")]
    public static void OpenWindow()
    {
        var w = GetWindow<StageEditorWindow>("Stage Editor");
        w.minSize = new Vector2(600, 400);   // 5. minimize 허용 — minSize 낮춤
    }

    void OnGUI()
    {
        Rect full = new Rect(0, 0, position.width, position.height);

        // ── 최소화 헤더 바 ────────────────────────────────────
        Rect headerBar = new Rect(0, 0, full.width, MINIMIZED_HEIGHT);
        EditorGUI.DrawRect(headerBar, new Color(0.2f, 0.2f, 0.2f));
        GUI.Label(new Rect(8, 4, 200, 20), "Stage Editor", EditorStyles.boldLabel);
        if (GUI.Button(new Rect(full.width - 30, 2, 24, 20), isMinimized ? "▲" : "▼"))
        {
            isMinimized = !isMinimized;
            position = new Rect(position.x, position.y, position.width,
                isMinimized ? MINIMIZED_HEIGHT : Mathf.Max(400, position.height));
        }
        if (isMinimized) return;

        HandleShortcuts();

        // ── 이하 기존 레이아웃 ────────────────────────────────
        Rect leftRect = new Rect(0, MINIMIZED_HEIGHT, panelWidth, full.height - MINIMIZED_HEIGHT);
        Rect rightRect = new Rect(panelWidth + 4, MINIMIZED_HEIGHT,
            full.width - panelWidth - 4, full.height - MINIMIZED_HEIGHT);

        // 구분선 + 리사이즈 핸들
        Rect divider = new Rect(panelWidth, MINIMIZED_HEIGHT, 4, full.height - MINIMIZED_HEIGHT);
        EditorGUI.DrawRect(divider, new Color(0.25f, 0.25f, 0.25f));
        EditorGUIUtility.AddCursorRect(divider, MouseCursor.ResizeHorizontal);

        HandlePanelResize(divider);

        GUILayout.BeginArea(leftRect);
        DrawLeftPanel();
        GUILayout.EndArea();

        if (rightRect.width > 10)
            DrawViewport(rightRect);

        if (isResizingPanel) Repaint();
    }

    // ── 패널 리사이즈 ─────────────────────────────────────────
    void HandlePanelResize(Rect divider)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && divider.Contains(e.mousePosition))
        {
            isResizingPanel = true;
            e.Use();
        }
        if (isResizingPanel)
        {
            if (e.type == EventType.MouseDrag)
            {
                panelWidth = Mathf.Clamp(e.mousePosition.x, PANEL_MIN, PANEL_MAX);
                e.Use();
            }
            if (e.type == EventType.MouseUp)
            {
                isResizingPanel = false;
                e.Use();
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    // 왼쪽 패널
    // ════════════════════════════════════════════════════════════
    void DrawLeftPanel()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawHeader();
        DrawDivider();
        DrawSnapSettings();
        DrawDivider();
        DrawStageSettings();
        DrawDivider();
        DrawSpawnAndGoal();
        DrawDivider();
        DrawMapPieceList();
        DrawDivider();
        DrawGlobalObjectList();
        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        EditorGUILayout.LabelField("Stage Editor", EditorStyles.boldLabel);
        savePath = EditorGUILayout.TextField("Save Path", savePath);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New")) NewStage();
        if (GUILayout.Button("Load")) LoadFromFile();
        if (GUILayout.Button("Save")) SaveToFile();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
        testScenePath = EditorGUILayout.TextField("Test Scene", testScenePath);
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("▶  Play Test")) PlayTest();
        GUI.backgroundColor = Color.white;
    }

    void DrawSnapSettings()
    {
        EditorGUILayout.LabelField("Snap Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Position Snap", GUILayout.Width(110));
        snapPosition = Mathf.Round(EditorGUILayout.Slider(snapPosition, 0.1f, 2f) / 0.1f) * 0.1f;
        EditorGUILayout.LabelField(snapPosition.ToString("F1"), GUILayout.Width(32));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Size Snap", GUILayout.Width(110));
        snapSize = Mathf.Round(EditorGUILayout.Slider(snapSize, 0.1f, 2f) / 0.1f) * 0.1f;
        EditorGUILayout.LabelField(snapSize.ToString("F1"), GUILayout.Width(32));
        EditorGUILayout.EndHorizontal();
    }

    void DrawStageSettings()
    {
        EditorGUILayout.LabelField("Stage Settings", EditorStyles.boldLabel);
        stageData.stageName = EditorGUILayout.TextField("Stage Name", stageData.stageName);

        // 4. 카메라 높이 — Build 시 Camera.orthographicSize에 반영
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Camera Height", GUILayout.Width(120));
        if (GUILayout.Button("-", GUILayout.Width(22)))
            stageData.cameraHeight = Mathf.Max(snapSize, stageData.cameraHeight - snapSize);
        stageData.cameraHeight = Mathf.Max(snapSize,
            SnapF(EditorGUILayout.FloatField(stageData.cameraHeight, GUILayout.Width(60)), snapSize));
        if (GUILayout.Button("+", GUILayout.Width(22))) stageData.cameraHeight += snapSize;
        EditorGUILayout.LabelField("(orthographicSize = h/2)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    void DrawSpawnAndGoal()
    {
        EditorGUILayout.LabelField("Spawn & Goal", EditorStyles.boldLabel);

        // 조각 ID 목록 (드롭다운용)
        string[] pieceIds = GetPieceIdOptions();
        int[] pieceIndices = GetPieceIndexOptions();

        // ── 스폰 포인트 ──
        EditorGUILayout.LabelField("Spawn Point", EditorStyles.miniBoldLabel);
        int spawnIdx = PieceIdToDropdownIndex(stageData.spawnPieceId, pieceIds);
        int newSpawnIdx = EditorGUILayout.Popup("  Piece", spawnIdx, pieceIds);
        stageData.spawnPieceId = DropdownIndexToPieceId(newSpawnIdx, pieceIds);
        stageData.spawnLocalPosition = DrawV2Field("  Local Pos", stageData.spawnLocalPosition, snapPosition);

        EditorGUILayout.Space(4);

        // ── 골 지점 ──
        EditorGUILayout.LabelField("Goal Point", EditorStyles.miniBoldLabel);
        int goalIdx = PieceIdToDropdownIndex(stageData.goalPieceId, pieceIds);
        int newGoalIdx = EditorGUILayout.Popup("  Piece", goalIdx, pieceIds);
        stageData.goalPieceId = DropdownIndexToPieceId(newGoalIdx, pieceIds);
        stageData.goalLocalPosition = DrawV2Field("  Local Pos", stageData.goalLocalPosition, snapPosition);
    }

    // ── 드롭다운 헬퍼 ─────────────────────────────────────────
    string[] GetPieceIdOptions()
    {
        var ids = new System.Collections.Generic.List<string> { "(없음)" };
        foreach (var p in stageData.mapPieces)
            ids.Add(string.IsNullOrEmpty(p.id) ? $"(unnamed)" : p.id);
        return ids.ToArray();
    }

    int[] GetPieceIndexOptions()
    {
        int[] arr = new int[stageData.mapPieces.Count + 1];
        for (int i = 0; i < arr.Length; i++) arr[i] = i;
        return arr;
    }

    int PieceIdToDropdownIndex(string pieceId, string[] options)
    {
        if (string.IsNullOrEmpty(pieceId)) return 0;
        for (int i = 1; i < options.Length; i++)
            if (options[i] == pieceId) return i;
        return 0;
    }

    string DropdownIndexToPieceId(int index, string[] options)
    {
        if (index <= 0 || index >= options.Length) return "";
        return options[index];
    }

    void DrawMapPieceList()
    {
        EditorGUILayout.LabelField($"Map Pieces ({stageData.mapPieces.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Add Map Piece")) AddMapPiece();
        for (int i = 0; i < stageData.mapPieces.Count; i++)
            if (!DrawMapPiece(i)) i--;
    }

    bool DrawMapPiece(int i)
    {
        var piece = stageData.mapPieces[i];
        bool sel = selectedPieceIndex == i;

        Color bg = GUI.backgroundColor;
        GUI.backgroundColor = sel ? new Color(0.7f, 0.9f, 1f) : Color.white;
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.backgroundColor = bg;

        if (GUILayout.Button((sel ? "▼ " : "▶ ") + $"[{i}] {piece.id}", EditorStyles.boldLabel))
        { selectedPieceIndex = sel ? -1 : i; selectedPlatformIndex = -1; }

        switch (piece.GetPieceType())
        {
            case MapPieceType.Fixed:
                GUI.color = new Color(1f, 0.7f, 0.3f);
                GUILayout.Label("FIXED", GUILayout.Width(38));
                GUI.color = Color.white;
                break;
            case MapPieceType.Pinned:
                GUI.color = new Color(0.6f, 0.9f, 0.6f);
                GUILayout.Label("PIN", GUILayout.Width(30));
                GUI.color = Color.white;
                break;
            case MapPieceType.Flip:
                GUI.color = new Color(0.6f, 0.85f, 1f);
                GUILayout.Label("FLIP", GUILayout.Width(34));
                GUI.color = Color.white;
                break;
        }

        if (GUILayout.Button("✕", GUILayout.Width(22)))
        {
            RecordUndo();

            stageData.mapPieces.RemoveAt(i);
            if (selectedPieceIndex == i) selectedPieceIndex = -1;
            EditorGUILayout.EndHorizontal();
            return false;
        }
        EditorGUILayout.EndHorizontal();
        if (!sel) return true;

        EditorGUI.indentLevel++;
        piece.id = EditorGUILayout.TextField("ID", piece.id);

        piece.isVisible = EditorGUILayout.Toggle("Is Visible", piece.isVisible);

        // isVisible이 꺼지면 모든 동작 타입 강제 해제
        if (!piece.isVisible)
            piece.SetPieceType(MapPieceType.Fixed);

        // ── 동작 타입 드롭다운 ──
        // isVisible이 꺼져 있으면 타입 변경 불가
        GUI.enabled = piece.isVisible;
        MapPieceType currentType = piece.GetPieceType();
        MapPieceType newType = (MapPieceType)EditorGUILayout.EnumPopup("Piece Type", currentType);
        if (newType != currentType)
            piece.SetPieceType(newType);
        GUI.enabled = true;

        // ── 타입별 추가 설정 ──
        if (piece.isPinned)
        {
            piece.pinLocalPosition = DrawV2Field(
                "Pin Position", piece.pinLocalPosition, snapPosition);
        }

        if (piece.isFlippable)
        {
            // Flip Axis 드롭다운
            string[] axisOptions = { "Y  (좌우 반전)", "X  (상하 반전)" };
            int axisIdx = piece.flipAxis == "X" ? 1 : 0;
            int newAxisIdx = EditorGUILayout.Popup("Flip Axis", axisIdx, axisOptions);
            piece.flipAxis = newAxisIdx == 1 ? "X" : "Y";

            piece.startFlipped = EditorGUILayout.Toggle("Start Flipped", piece.startFlipped);
        }

        piece.position = DrawV2Field("Position", piece.position, snapPosition);
        piece.colliderSize = DrawV2FieldPos("Collider Size", piece.colliderSize, snapSize);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"Piece Objects ({piece.pieceObjects.Count})", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("  + Platform")) AddPieceObject(piece, PieceObjectType.Platform);
        if (GUILayout.Button("  + Moving Platform")) AddPieceObject(piece, PieceObjectType.MovingPlatform);
        if (GUILayout.Button("  + 2x Jump Item")) AddPieceObject(piece, PieceObjectType.DoubleJumpItem);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("  + Hazard")) AddPieceObject(piece, PieceObjectType.Hazard);
        if (GUILayout.Button("  + Moving Hazard")) AddPieceObject(piece, PieceObjectType.MovingHazard);
        if (GUILayout.Button("  + Blocker")) AddPieceObject(piece, PieceObjectType.Blocker);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("  + Laser")) AddPieceObject(piece, PieceObjectType.LaserEmitter);
        EditorGUILayout.EndHorizontal();


        for (int j = 0; j < piece.pieceObjects.Count; j++)
            if (!DrawPieceObject(piece, j)) j--;

        EditorGUI.indentLevel--;
        return true;
    }


    bool DrawPieceObject(MapPieceData piece, int j)
    {
        var obj = piece.pieceObjects[j];
        bool sel = selectedPlatformIndex == 1000 + j;
        string typeLabel = obj.type.ToString();

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.color = new Color(0.6f, 1f, 0.8f);
        if (GUILayout.Button((sel ? "  ▼ " : "  ▶ ") + $"[{j}] {typeLabel} {obj.id}"))
            selectedPlatformIndex = sel ? -1 : 1000 + j;
        GUI.color = Color.white;
        if (GUILayout.Button("✕", GUILayout.Width(22)))
        {
            RecordUndo();

            piece.pieceObjects.RemoveAt(j);
            EditorGUILayout.EndHorizontal();
            return false;
        }
        EditorGUILayout.EndHorizontal();
        if (!sel) return true;

        EditorGUI.indentLevel += 2;
        obj.id = EditorGUILayout.TextField("ID", obj.id);
        obj.localPosition = DrawV2Field("Local Position", obj.localPosition, snapPosition);

        switch (obj.type)
        {
            case PieceObjectType.Platform:
                obj.platform.size = DrawV2FieldPos("Size", obj.platform.size, snapSize);
                break;
            case PieceObjectType.MovingPlatform:
                DrawMovingPlatformData(obj.movingPlatform);
                break;
            case PieceObjectType.DoubleJumpItem:
                break;
            case PieceObjectType.Blocker:
                obj.blocker.size = DrawV2FieldPos("Size", obj.blocker.size, snapSize);
                break;
            case PieceObjectType.Hazard:
                obj.hazard.size = DrawV2FieldPos("Size", obj.hazard.size, snapSize);
                break;
            case PieceObjectType.MovingHazard:
                DrawMovingPlatformData(obj.movingPlatform);
                break;
            case PieceObjectType.LaserEmitter:
                obj.laserEmitter.direction = (LaserDirection)EditorGUILayout.EnumPopup("Direction", obj.laserEmitter.direction);
                break;
        }
        EditorGUI.indentLevel -= 2;
        return true;
    }

    void DrawMovingPlatformData(MovingPlatformData mp)
    {
        mp.size = DrawV2FieldPos("Size", mp.size, snapSize);
        mp.mode = (MovingPlatformMode)EditorGUILayout.EnumPopup("Mode", mp.mode);
        mp.speed = SnapF(EditorGUILayout.FloatField("Speed", mp.speed), 0.1f);
        mp.waitTime = SnapF(EditorGUILayout.FloatField("Wait Time", mp.waitTime), 0.1f);

        if (mp.mode == MovingPlatformMode.PingPong)
        {
            mp.pointA = DrawV2Field("Point A", mp.pointA, snapPosition);
            mp.pointB = DrawV2Field("Point B", mp.pointB, snapPosition);
        }
        else
        {
            mp.pointA = DrawV2Field("Start Point", mp.pointA, snapPosition);
            EditorGUILayout.LabelField("Loop Points", EditorStyles.miniBoldLabel);
            for (int k = 0; k < mp.loopPoints.Count; k++)
            {
                EditorGUILayout.BeginHorizontal();
                mp.loopPoints[k] = DrawV2Field($"  Point {k + 1}", mp.loopPoints[k], snapPosition);
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                { mp.loopPoints.RemoveAt(k); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("    + Add Loop Point"))
                mp.loopPoints.Add(new SerializableVector2(0, 0));
        }
    }

    void AddPieceObject(MapPieceData piece, PieceObjectType type)
    {
        RecordUndo();

        piece.pieceObjects.Add(new PieceObjectData
        {
            id = $"{type}_{piece.pieceObjects.Count}",
            type = type,
            localPosition = new SerializableVector2(0, 0),
            movingPlatform = new MovingPlatformData()
        });
        selectedPlatformIndex = 1000 + piece.pieceObjects.Count - 1;
    }

    void DrawGlobalObjectList()
    {
        EditorGUILayout.LabelField($"Global Objects ({stageData.globalObjects.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Add Text")) AddGlobalObject(GlobalObjectType.Text);
        if (GUILayout.Button("+ Add Hazard")) AddGlobalObject(GlobalObjectType.Hazard);

        for (int i = 0; i < stageData.globalObjects.Count; i++)
            if (!DrawGlobalObject(i)) i--;
    }

    bool DrawGlobalObject(int i)
    {
        var obj = stageData.globalObjects[i];
        bool sel = selectedPieceIndex == 2000 + i;

        Color bg = GUI.backgroundColor;
        GUI.backgroundColor = sel ? new Color(1f, 1f, 0.7f) : Color.white;
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.backgroundColor = bg;

        GUI.color = new Color(1f, 0.9f, 0.5f);
        if (GUILayout.Button((sel ? "▼ " : "▶ ") + $"[{i}] {obj.type} {obj.id}", EditorStyles.boldLabel))
            selectedPieceIndex = sel ? -1 : 2000 + i;
        GUI.color = Color.white;

        if (GUILayout.Button("✕", GUILayout.Width(22)))
        {
            stageData.globalObjects.RemoveAt(i);
            if (selectedPieceIndex == 2000 + i) selectedPieceIndex = -1;
            EditorGUILayout.EndHorizontal();
            return false;
        }
        EditorGUILayout.EndHorizontal();
        if (!sel) return true;

        EditorGUI.indentLevel++;
        obj.id = EditorGUILayout.TextField("ID", obj.id);
        obj.position = DrawV2Field("Position", obj.position, snapPosition);

        switch (obj.type)
        {
            case GlobalObjectType.Text:
                obj.text.content = EditorGUILayout.TextField("Content", obj.text.content);
                obj.text.fontSize = SnapF(EditorGUILayout.FloatField("Font Size", obj.text.fontSize), 0.1f);
                break;
            case GlobalObjectType.Hazard:
                obj.hazard.size = DrawV2FieldPos("Size", obj.hazard.size, snapSize);
                break;
        }
        EditorGUI.indentLevel--;
        return true;
    }

    void AddGlobalObject(GlobalObjectType type)
    {
        stageData.globalObjects.Add(new GlobalObjectData
        {
            id = $"{type}_{stageData.globalObjects.Count}",
            type = type,
            position = new SerializableVector2(0, 0),
            text = new TextObjectData()
        });
        selectedPieceIndex = 2000 + stageData.globalObjects.Count - 1;
    }

    // ════════════════════════════════════════════════════════════
    // 뷰포트
    // ════════════════════════════════════════════════════════════
    void DrawViewport(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
        HandleViewportInput(rect);
        DrawGrid(rect);
        DrawCameraBounds(rect);
        DrawStageElements(rect);

        GUI.color = new Color(1, 1, 1, 0.35f);
        GUI.Label(new Rect(rect.x + 8, rect.yMax - 20, 420, 18),
            "드래그: 이동  |  Alt+드래그 / 중간버튼: 팬  |  스크롤: 줌");
        GUI.color = Color.white;
    }

    // 6. 그리드 — WorldToViewport(0,0)을 기준으로 선을 그어 (0,0)이 항상 교점에 오도록
    void DrawGrid(Rect rect)
    {
        float unit = viewScale;
        // (0,0) 월드 좌표의 뷰포트 픽셀 위치
        Vector2 origin = WorldToViewport(Vector2.zero, rect);

        Handles.color = new Color(1, 1, 1, 0.05f);

        // 세로선: origin.x 기준으로 unit 간격
        float ox = ((origin.x - rect.x) % unit + unit) % unit;
        for (float x = ox; x < rect.width; x += unit)
            Handles.DrawLine(new Vector3(rect.x + x, rect.y), new Vector3(rect.x + x, rect.yMax));

        // 가로선: origin.y 기준으로 unit 간격
        float oy = ((origin.y - rect.y) % unit + unit) % unit;
        for (float y = oy; y < rect.height; y += unit)
            Handles.DrawLine(new Vector3(rect.x, rect.y + y), new Vector3(rect.xMax, rect.y + y));

        // 원점 축 강조
        Handles.color = new Color(1, 1, 1, 0.18f);
        if (origin.x >= rect.x && origin.x <= rect.xMax)
            Handles.DrawLine(new Vector3(origin.x, rect.y), new Vector3(origin.x, rect.yMax));
        if (origin.y >= rect.y && origin.y <= rect.yMax)
            Handles.DrawLine(new Vector3(rect.x, origin.y), new Vector3(rect.xMax, origin.y));
    }

    // 3. 카메라 경계 — stageData.cameraHeight와 뷰포트 aspect가 아닌
    //    실제 게임 해상도 비율(16:9 기본)을 사용
    void DrawCameraBounds(Rect rect)
    {
        float h = stageData.cameraHeight;
        // 게임 해상도 비율 사용 (PlayerSettings 기준, 기본 16:9)
        float aspect = (float)PlayerSettings.defaultScreenWidth / PlayerSettings.defaultScreenHeight;
        float w = h * aspect;

        Rect camVP = WorldRectToViewport(new Vector2(-w * 0.5f, -h * 0.5f), new Vector2(w, h), rect);

        // 카메라 밖 어둡게
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, Mathf.Max(0, camVP.y - rect.y)), new Color(0, 0, 0, 0.35f));
        EditorGUI.DrawRect(new Rect(rect.x, camVP.yMax, rect.width, Mathf.Max(0, rect.yMax - camVP.yMax)), new Color(0, 0, 0, 0.35f));
        EditorGUI.DrawRect(new Rect(rect.x, camVP.y, Mathf.Max(0, camVP.x - rect.x), camVP.height), new Color(0, 0, 0, 0.35f));
        EditorGUI.DrawRect(new Rect(camVP.xMax, camVP.y, Mathf.Max(0, rect.xMax - camVP.xMax), camVP.height), new Color(0, 0, 0, 0.35f));

        Handles.color = new Color(1f, 0.9f, 0.3f, 0.85f);
        DrawRectOutline(camVP);
        GUI.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        GUI.Label(new Rect(camVP.x + 4, camVP.y + 2, 200, 18),
            $"Camera  {w:F1} × {h:F1}  (aspect {aspect:F2})");
        GUI.color = Color.white;
    }

    void DrawStageElements(Rect rect)
    {
        for (int i = 0; i < stageData.mapPieces.Count; i++)
        {
            var piece = stageData.mapPieces[i];
            bool selPiece = selectedPieceIndex == i;
            Vector2 pos = piece.position.ToVector2();
            Vector2 size = piece.colliderSize.ToVector2();
            Rect pieceRect = WorldRectToViewport(pos - size * 0.5f, size, rect);

            Color fill;
            switch (piece.GetPieceType())
            {
                case MapPieceType.Movable:
                    fill = selPiece ? new Color(0.4f, 0.6f, 0.9f, 0.35f) : new Color(0.3f, 0.4f, 0.6f, 0.25f);
                    break;
                case MapPieceType.Pinned:
                    fill = selPiece ? new Color(0.4f, 0.9f, 0.5f, 0.35f) : new Color(0.3f, 0.6f, 0.35f, 0.25f);
                    break;
                case MapPieceType.Flip:
                    fill = selPiece ? new Color(0.3f, 0.8f, 1f, 0.35f) : new Color(0.2f, 0.55f, 0.75f, 0.25f);
                    break;
                default: // Fixed
                    fill = new Color(0.5f, 0.4f, 0.2f, 0.25f);
                    break;
            }
            EditorGUI.DrawRect(pieceRect, fill);
            Handles.color = selPiece ? new Color(0.5f, 0.8f, 1f, 0.9f) : new Color(0.5f, 0.6f, 0.8f, 0.6f);
            DrawRectOutline(pieceRect);
            GUI.color = new Color(1, 1, 1, 0.7f);
            GUI.Label(new Rect(pieceRect.x + 3, pieceRect.y + 2, pieceRect.width, 16),
                piece.id + piece.GetPieceType() switch { MapPieceType.Fixed => " [FIXED]", MapPieceType.Pinned => " [PIN]", MapPieceType.Flip => " [FLIP]", _ => "" });
            GUI.color = Color.white;

            // 조각 종속 오브젝트 (Platform, MovingPlatform 등)
            foreach (var pieceObj in piece.pieceObjects)
            {
                Vector2 objWorld = pos + pieceObj.localPosition.ToVector2();

                if (pieceObj.type == PieceObjectType.Platform)
                {
                    bool selPlat = selPiece && selectedPlatformIndex == 1000 + piece.pieceObjects.IndexOf(pieceObj);
                    Rect platRect = WorldRectToViewport(
                        objWorld - pieceObj.platform.size.ToVector2() * 0.5f,
                        pieceObj.platform.size.ToVector2(), rect);
                    EditorGUI.DrawRect(platRect, selPlat ? new Color(1f, 1f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.9f));
                    Handles.color = selPlat ? Color.yellow : new Color(0.8f, 0.8f, 0.8f, 0.8f);
                    DrawRectOutline(platRect);
                }
                else if (pieceObj.type == PieceObjectType.MovingPlatform)
                {
                    var mp = pieceObj.movingPlatform;
                    if (mp.mode == MovingPlatformMode.PingPong)
                    {
                        Vector2 wA = objWorld + mp.pointA.ToVector2();
                        Vector2 wB = objWorld + mp.pointB.ToVector2();
                        Rect rA = WorldRectToViewport(wA - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                        Rect rB = WorldRectToViewport(wB - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                        EditorGUI.DrawRect(rA, new Color(0.4f, 0.8f, 1f, 0.7f));
                        EditorGUI.DrawRect(rB, new Color(0.4f, 0.8f, 1f, 0.3f));
                        Handles.color = new Color(0.4f, 0.8f, 1f, 0.8f);
                        DrawRectOutline(rA); DrawRectOutline(rB);
                        Vector2 vpA = WorldToViewport(wA, rect);
                        Vector2 vpB = WorldToViewport(wB, rect);
                        Handles.color = new Color(0.4f, 0.8f, 1f, 0.5f);
                        Handles.DrawDottedLine(new Vector3(vpA.x, vpA.y), new Vector3(vpB.x, vpB.y), 4f);
                    }
                    else
                    {
                        var pts = new System.Collections.Generic.List<Vector2>();
                        pts.Add(objWorld + mp.pointA.ToVector2());
                        foreach (var lp in mp.loopPoints) pts.Add(objWorld + lp.ToVector2());
                        for (int k = 0; k < pts.Count; k++)
                        {
                            Rect rk = WorldRectToViewport(pts[k] - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                            EditorGUI.DrawRect(rk, new Color(0.4f, 0.8f, 1f, k == 0 ? 0.7f : 0.3f));
                            Handles.color = new Color(0.4f, 0.8f, 1f, 0.8f);
                            DrawRectOutline(rk);
                            Vector2 vpK = WorldToViewport(pts[k], rect);
                            Vector2 vpK1 = WorldToViewport(pts[(k + 1) % pts.Count], rect);
                            Handles.color = new Color(0.4f, 0.8f, 1f, 0.5f);
                            Handles.DrawDottedLine(new Vector3(vpK.x, vpK.y), new Vector3(vpK1.x, vpK1.y), 4f);
                        }
                    }
                }
                else if (pieceObj.type == PieceObjectType.DoubleJumpItem)
                {
                    Vector2 vpItem = WorldToViewport(objWorld, rect);
                    DrawCircleMarker(vpItem, 8f, new Color(0.5f, 0.3f, 1f, 0.9f), "2J", rect);
                }
                else if (pieceObj.type == PieceObjectType.Blocker)
                {
                    bool selBlocker = selPiece && selectedPlatformIndex == 1000 + piece.pieceObjects.IndexOf(pieceObj);
                    Rect blockerRect = WorldRectToViewport(
                        objWorld - pieceObj.blocker.size.ToVector2() * 0.5f,
                        pieceObj.blocker.size.ToVector2(), rect);
                    EditorGUI.DrawRect(blockerRect, selBlocker
                        ? new Color(0.3f, 0.8f, 0.3f, 0.8f)
                        : new Color(0.3f, 0.8f, 0.3f, 0.5f));
                    Handles.color = selBlocker ? Color.green : new Color(0.3f, 0.7f, 0.3f, 0.8f);
                    DrawRectOutline(blockerRect);
                }
                else if (pieceObj.type == PieceObjectType.Hazard)
                {
                    bool selHazard = selPiece && selectedPlatformIndex == 1000 + piece.pieceObjects.IndexOf(pieceObj);
                    Rect hazardRect = WorldRectToViewport(
                        objWorld - pieceObj.hazard.size.ToVector2() * 0.5f,
                        pieceObj.hazard.size.ToVector2(), rect);
                    EditorGUI.DrawRect(hazardRect, selHazard
                        ? new Color(1f, 0.2f, 0.2f, 0.8f)
                        : new Color(1f, 0.2f, 0.2f, 0.5f));
                    Handles.color = selHazard ? Color.red : new Color(1f, 0.3f, 0.3f, 0.8f);
                    DrawRectOutline(hazardRect);
                }
                else if (pieceObj.type == PieceObjectType.MovingHazard)
                {
                    var mp = pieceObj.movingPlatform;
                    if (mp.mode == MovingPlatformMode.PingPong)
                    {
                        Vector2 wA = objWorld + mp.pointA.ToVector2();
                        Vector2 wB = objWorld + mp.pointB.ToVector2();
                        Rect rA = WorldRectToViewport(wA - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                        Rect rB = WorldRectToViewport(wB - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                        EditorGUI.DrawRect(rA, new Color(1f, 0.4f, 0.1f, 0.7f));
                        EditorGUI.DrawRect(rB, new Color(1f, 0.4f, 0.1f, 0.3f));
                        Handles.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                        DrawRectOutline(rA); DrawRectOutline(rB);
                        Vector2 vpA = WorldToViewport(wA, rect);
                        Vector2 vpB = WorldToViewport(wB, rect);
                        Handles.color = new Color(1f, 0.4f, 0.1f, 0.5f);
                        Handles.DrawDottedLine(new Vector3(vpA.x, vpA.y), new Vector3(vpB.x, vpB.y), 4f);
                    }
                    else
                    {
                        var pts = new System.Collections.Generic.List<Vector2>();
                        pts.Add(objWorld + mp.pointA.ToVector2());
                        foreach (var lp in mp.loopPoints) pts.Add(objWorld + lp.ToVector2());
                        for (int k = 0; k < pts.Count; k++)
                        {
                            Rect rk = WorldRectToViewport(pts[k] - mp.size.ToVector2() * 0.5f, mp.size.ToVector2(), rect);
                            EditorGUI.DrawRect(rk, new Color(1f, 0.4f, 0.1f, k == 0 ? 0.7f : 0.3f));
                            Handles.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                            DrawRectOutline(rk);
                            Vector2 vpK = WorldToViewport(pts[k], rect);
                            Vector2 vpK1 = WorldToViewport(pts[(k + 1) % pts.Count], rect);
                            Handles.color = new Color(1f, 0.4f, 0.1f, 0.5f);
                            Handles.DrawDottedLine(new Vector3(vpK.x, vpK.y), new Vector3(vpK1.x, vpK1.y), 4f);
                        }
                    }
                }
                else if (pieceObj.type == PieceObjectType.LaserEmitter)
                {
                    // 발사 장치 1x1 박스
                    bool sel = selPiece && selectedPlatformIndex == 1000 + piece.pieceObjects.IndexOf(pieceObj);
                    Rect emitterRect = WorldRectToViewport(
                        objWorld - Vector2.one * 0.5f, Vector2.one, rect);
                    EditorGUI.DrawRect(emitterRect, sel
                        ? new Color(0.9f, 0.2f, 0.1f, 0.9f)
                        : new Color(0.9f, 0.2f, 0.1f, 0.6f));
                    Handles.color = sel ? Color.red : new Color(0.9f, 0.2f, 0.1f, 0.8f);
                    DrawRectOutline(emitterRect);

                    // 방향 화살표
                    Vector2 laserDir = pieceObj.laserEmitter.direction switch
                    {
                        LaserDirection.Right => Vector2.right,
                        LaserDirection.Left => Vector2.left,
                        LaserDirection.Up => Vector2.up,
                        LaserDirection.Down => Vector2.down,
                        _ => Vector2.right,
                    };

                    float arrowLen = 1.5f; // 월드 단위 화살표 길이
                    Vector2 arrowTip = objWorld + laserDir * (0.5f + arrowLen);
                    Vector2 arrowPerp = new Vector2(-laserDir.y, laserDir.x);

                    Vector2 vpBase = WorldToViewport(objWorld + laserDir * 0.5f, rect);
                    Vector2 vpTip = WorldToViewport(arrowTip, rect);
                    Vector2 vpL = WorldToViewport(arrowTip - laserDir * 0.4f + arrowPerp * 0.25f, rect);
                    Vector2 vpR = WorldToViewport(arrowTip - laserDir * 0.4f - arrowPerp * 0.25f, rect);

                    Handles.color = new Color(0.9f, 0.2f, 0.1f, sel ? 0.9f : 0.6f);
                    Handles.DrawLine(new Vector3(vpBase.x, vpBase.y), new Vector3(vpTip.x, vpTip.y));
                    Handles.DrawLine(new Vector3(vpTip.x, vpTip.y), new Vector3(vpL.x, vpL.y));
                    Handles.DrawLine(new Vector3(vpTip.x, vpTip.y), new Vector3(vpR.x, vpR.y));
                }
            }

            if (piece.isPinned)
            {
                Vector2 pinWorld = pos + piece.pinLocalPosition.ToVector2();
                Vector2 vpPin = WorldToViewport(pinWorld, rect);
                Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
                Handles.DrawSolidDisc(new Vector3(vpPin.x, vpPin.y, 0), Vector3.forward, 6f);
                Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
                Handles.DrawWireDisc(new Vector3(vpPin.x, vpPin.y, 0), Vector3.forward, 6f);
            }

            // Flip 아이콘: 조각 중앙에 뒤집기 축 방향 화살표 표시
            if (piece.isFlippable)
            {
                Vector2 vpCenter = WorldToViewport(pos, rect);
                // flipAxis "Y" = 좌우반전 = 가로 양방향 화살표
                // flipAxis "X" = 상하반전 = 세로 양방향 화살표
                bool isHoriz = piece.flipAxis != "X";
                float arrowHalf = 12f; // 픽셀
                Handles.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                Vector2 dir = isHoriz ? Vector2.right : Vector2.up;
                Vector2 perp = isHoriz ? Vector2.up : Vector2.right;
                Vector2 tipR = new Vector2(vpCenter.x + dir.x * arrowHalf, vpCenter.y - dir.y * arrowHalf);
                Vector2 tipL = new Vector2(vpCenter.x - dir.x * arrowHalf, vpCenter.y + dir.y * arrowHalf);
                Handles.DrawLine(new Vector3(tipL.x, tipL.y), new Vector3(tipR.x, tipR.y));
                // 화살촉
                float h = 5f;
                Handles.DrawLine(new Vector3(tipR.x, tipR.y),
                    new Vector3(tipR.x - dir.x * h - perp.x * h * 0.5f, tipR.y + dir.y * h + perp.y * h * 0.5f));
                Handles.DrawLine(new Vector3(tipR.x, tipR.y),
                    new Vector3(tipR.x - dir.x * h + perp.x * h * 0.5f, tipR.y + dir.y * h - perp.y * h * 0.5f));
                Handles.DrawLine(new Vector3(tipL.x, tipL.y),
                    new Vector3(tipL.x + dir.x * h - perp.x * h * 0.5f, tipL.y - dir.y * h + perp.y * h * 0.5f));
                Handles.DrawLine(new Vector3(tipL.x, tipL.y),
                    new Vector3(tipL.x + dir.x * h + perp.x * h * 0.5f, tipL.y - dir.y * h - perp.y * h * 0.5f));
            }
        }

        Vector2 spawnWorld = GetMarkerWorldPos(stageData.spawnPieceId, stageData.spawnLocalPosition);
        Vector2 spawnVP = WorldToViewport(spawnWorld, rect);
        DrawCircleMarker(spawnVP, 8f, new Color(0.2f, 1f, 0.3f, 0.9f), "S", rect);

        Vector2 goalWorld = GetMarkerWorldPos(stageData.goalPieceId, stageData.goalLocalPosition);
        Vector2 goalVP = WorldToViewport(goalWorld, rect);
        DrawDiamondMarker(goalVP, 10f, new Color(1f, 0.85f, 0.1f, 0.9f), "G", rect);

        // 글로벌 오브젝트 (텍스트 등)
        for (int i = 0; i < stageData.globalObjects.Count; i++)
        {
            var obj = stageData.globalObjects[i];
            bool selObj = selectedPieceIndex == 2000 + i;
            Vector2 objVP = WorldToViewport(obj.position.ToVector2(), rect);

            if (obj.type == GlobalObjectType.Text)
            {
                Rect textRect = WorldRectToViewport(
                    obj.position.ToVector2() - new Vector2(2f, 0.5f), // 고정 크기로 표시
                    new Vector2(4f, 1f), rect);
                EditorGUI.DrawRect(textRect, selObj
                    ? new Color(1f, 1f, 0.3f, 0.3f)
                    : new Color(1f, 1f, 0.5f, 0.15f));
                Handles.color = selObj ? Color.yellow : new Color(1f, 1f, 0.5f, 0.6f);
                DrawRectOutline(textRect);
                GUI.color = new Color(1f, 1f, 0.5f, 0.85f);
                GUI.Label(new Rect(textRect.x + 2, textRect.y + 2, textRect.width, 16),
                    $"T: {obj.text.content}");
                GUI.color = Color.white;
            }
            else if (obj.type == GlobalObjectType.Hazard)
            {
                Rect hazardRect = WorldRectToViewport(
                    obj.position.ToVector2() - obj.hazard.size.ToVector2() * 0.5f,
                    obj.hazard.size.ToVector2(), rect);
                EditorGUI.DrawRect(hazardRect, selObj
                    ? new Color(1f, 0.2f, 0.2f, 0.8f)
                    : new Color(1f, 0.2f, 0.2f, 0.5f));
                Handles.color = selObj ? Color.red : new Color(1f, 0.3f, 0.3f, 0.8f);
                DrawRectOutline(hazardRect);
            }
        }
    }

    void DrawCircleMarker(Vector2 c, float r, Color col, string label, Rect clip)
    {
        if (!clip.Contains(c)) return;
        Handles.color = col;
        Handles.DrawSolidDisc(new Vector3(c.x, c.y), Vector3.forward, r);
        Handles.color = Color.white;
        Handles.DrawWireDisc(new Vector3(c.x, c.y), Vector3.forward, r);
        GUI.color = Color.black;
        GUI.Label(new Rect(c.x - 4, c.y - 7, 20, 14), label);
        GUI.color = Color.white;
    }

    void DrawDiamondMarker(Vector2 c, float r, Color col, string label, Rect clip)
    {
        if (!clip.Contains(c)) return;
        Handles.color = col;
        Handles.DrawAAConvexPolygon(
            new Vector3(c.x, c.y - r),
            new Vector3(c.x + r, c.y),
            new Vector3(c.x, c.y + r),
            new Vector3(c.x - r, c.y));
        Handles.color = Color.white;
        Handles.DrawPolyLine(
            new Vector3(c.x, c.y - r), new Vector3(c.x + r, c.y),
            new Vector3(c.x, c.y + r), new Vector3(c.x - r, c.y),
            new Vector3(c.x, c.y - r));
        GUI.color = Color.black;
        GUI.Label(new Rect(c.x - 4, c.y - 7, 20, 14), label);
        GUI.color = Color.white;
    }

    void DrawRectOutline(Rect r)
    {
        Handles.DrawPolyLine(
            new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin),
            new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax),
            new Vector3(r.xMin, r.yMin));
    }

    // ── 뷰포트 입력 ───────────────────────────────────────────
    void HandleViewportInput(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;

        if (e.type == EventType.ScrollWheel)
        {
            float zd = -e.delta.y * 0.05f;
            viewScale = Mathf.Clamp(viewScale * (1 + zd), 8f, 150f);
            e.Use(); Repaint();
        }

        bool panBtn = e.button == 2 || (e.button == 0 && e.alt);
        if (e.type == EventType.MouseDown && panBtn)
        { isPanning = true; panStart = e.mousePosition; panOffsetStart = viewOffset; e.Use(); }
        if (e.type == EventType.MouseDrag && isPanning)
        { viewOffset = panOffsetStart + (e.mousePosition - panStart); e.Use(); Repaint(); }
        if (e.type == EventType.MouseUp && isPanning)
        { isPanning = false; e.Use(); }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        { didDrag = false; TryStartDrag(ViewportToWorld(e.mousePosition, rect)); e.Use(); }
        if (e.type == EventType.MouseDrag && e.button == 0 && dragTarget != DragTarget.None)
        { didDrag = true; ApplyDrag(dragStartObjPos + (ViewportToWorld(e.mousePosition, rect) - dragStartWorld)); e.Use(); Repaint(); }
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (!didDrag && dragTarget == DragTarget.Piece)
                TryCycleSelection(ViewportToWorld(e.mousePosition, rect));
            dragTarget = DragTarget.None; e.Use();
        }
    }

    void TryStartDrag(Vector2 wm)
    {
        float pr = 10f / viewScale;
        Vector2 spawnWorld = GetMarkerWorldPos(stageData.spawnPieceId, stageData.spawnLocalPosition);
        if (Vector2.Distance(wm, spawnWorld) < pr)
        { RecordUndo(); dragTarget = DragTarget.Spawn; dragStartWorld = wm; dragStartObjPos = stageData.spawnLocalPosition.ToVector2(); return; }
        Vector2 goalWorld = GetMarkerWorldPos(stageData.goalPieceId, stageData.goalLocalPosition);
        if (Vector2.Distance(wm, goalWorld) < pr)
        { RecordUndo(); dragTarget = DragTarget.Goal; dragStartWorld = wm; dragStartObjPos = stageData.goalLocalPosition.ToVector2(); return; }

        for (int i = 0; i < stageData.globalObjects.Count; i++)
        {
            var obj = stageData.globalObjects[i];
            Vector2 objSize = obj.type == GlobalObjectType.Hazard
                ? obj.hazard.size.ToVector2()
                : new Vector2(4f, 1f); // Text 기본 크기

            if (InRect(wm, obj.position.ToVector2(), objSize))
            {
                RecordUndo();

                dragTarget = DragTarget.Platform; // Global 오브젝트 드래그에 재사용
                dragPieceIndex = -1;
                dragPlatformIndex = -(i + 1); // 음수로 글로벌 오브젝트 구분
                dragStartWorld = wm;
                dragStartObjPos = obj.position.ToVector2();
                selectedPieceIndex = 2000 + i;
                return;
            }
        }

        for (int i = 0; i < stageData.mapPieces.Count; i++)
        {
            var piece = stageData.mapPieces[i];
            Vector2 pp = piece.position.ToVector2();
            for (int j = 0; j < piece.pieceObjects.Count; j++)
            {
                var obj = piece.pieceObjects[j];
                Vector2 objSize = obj.type == PieceObjectType.Platform
                    ? obj.platform.size.ToVector2()
                    : obj.type == PieceObjectType.MovingPlatform
                        ? obj.movingPlatform.size.ToVector2()
                        : obj.type == PieceObjectType.Blocker
                            ? obj.blocker.size.ToVector2()
                            : obj.type == PieceObjectType.Hazard
                                ? obj.hazard.size.ToVector2()
                                : obj.type == PieceObjectType.MovingHazard
                                    ? obj.movingPlatform.size.ToVector2()
                                    : obj.type == PieceObjectType.LaserEmitter
                                        ? Vector2.one  // 1x1 고정
                                        : new Vector2(0.5f, 0.5f);

                if (InRect(wm, pp + obj.localPosition.ToVector2(), objSize))
                {
                    RecordUndo();

                    dragTarget = DragTarget.Platform; dragPieceIndex = i; dragPlatformIndex = j;
                    dragStartWorld = wm; dragStartObjPos = obj.localPosition.ToVector2();
                    selectedPieceIndex = i; selectedPlatformIndex = 1000 + j; return;
                }
            }
        }
        // ── MapPiece 드래그 준비 ──
        // 현재 선택 조각이 클릭 위치에 있으면 선택 변경 없이 드래그 준비
        // 없으면 즉시 첫 번째 히트 조각으로 선택 변경
        var hitPiecesDown = new List<int>();
        for (int i = 0; i < stageData.mapPieces.Count; i++)
        {
            var piece = stageData.mapPieces[i];
            if (InRect(wm, piece.position.ToVector2(), piece.colliderSize.ToVector2()))
                hitPiecesDown.Add(i);
        }

        if (hitPiecesDown.Count == 0) return;

        bool currentIsHit = hitPiecesDown.Contains(selectedPieceIndex) && selectedPlatformIndex < 0;
        int dragIdx = currentIsHit ? selectedPieceIndex : hitPiecesDown[0];

        if (!currentIsHit)
        {
            // 새 조각 즉시 선택
            selectedPieceIndex = dragIdx;
            selectedPlatformIndex = -1;
        }

        RecordUndo();
        dragTarget = DragTarget.Piece; dragPieceIndex = dragIdx;
        dragStartWorld = wm; dragStartObjPos = stageData.mapPieces[dragIdx].position.ToVector2();
    }

    // MouseUp 시 드래그 없이 클릭만 한 경우 → 순환 선택
    void TryCycleSelection(Vector2 wm)
    {
        var hitPieces = new List<int>();
        for (int i = 0; i < stageData.mapPieces.Count; i++)
        {
            var piece = stageData.mapPieces[i];
            if (InRect(wm, piece.position.ToVector2(), piece.colliderSize.ToVector2()))
                hitPieces.Add(i);
        }

        if (hitPieces.Count <= 1) return; // 단독 히트면 순환 불필요

        // 현재 선택이 히트 목록에 있으면 다음으로 순환
        int posInHit = hitPieces.IndexOf(selectedPieceIndex);
        if (posInHit >= 0)
        {
            int nextIdx = hitPieces[(posInHit + 1) % hitPieces.Count];
            selectedPieceIndex = nextIdx;
            selectedPlatformIndex = -1;
            Repaint();
        }
    }

    void ApplyDrag(Vector2 newWorldPos)
    {
        Vector2 s = SnapV2(newWorldPos, snapPosition);
        switch (dragTarget)
        {
            case DragTarget.Spawn:
                // 월드 좌표 → 종속 조각 기준 로컬 좌표로 변환
                stageData.spawnLocalPosition = SerializableVector2.FromVector2(
                    WorldToLocal(s, stageData.spawnPieceId));
                break;
            case DragTarget.Goal:
                stageData.goalLocalPosition = SerializableVector2.FromVector2(
                    WorldToLocal(s, stageData.goalPieceId));
                break;
            case DragTarget.Piece:
                stageData.mapPieces[dragPieceIndex].position = SerializableVector2.FromVector2(s);
                break;
            case DragTarget.Platform:
                if (dragPlatformIndex < 0) // 글로벌 오브젝트
                {
                    int globalIdx = -(dragPlatformIndex + 1);
                    stageData.globalObjects[globalIdx].position = SerializableVector2.FromVector2(s);
                }
                else // 피스 오브젝트
                {
                    stageData.mapPieces[dragPieceIndex].pieceObjects[dragPlatformIndex].localPosition = SerializableVector2.FromVector2(s);
                }
                break;
        }
    }

    // 월드 좌표를 pieceId 조각 기준 로컬 좌표로 변환
    // pieceId가 없으면 월드 좌표 그대로 반환
    Vector2 WorldToLocal(Vector2 worldPos, string pieceId)
    {
        if (!string.IsNullOrEmpty(pieceId))
        {
            foreach (var piece in stageData.mapPieces)
                if (piece.id == pieceId)
                    return worldPos - piece.position.ToVector2();
        }
        return worldPos;
    }

    // pieceId 조각의 월드 위치 + 로컬 오프셋
    Vector2 GetMarkerWorldPos(string pieceId, SerializableVector2 localPos)
    {
        if (!string.IsNullOrEmpty(pieceId))
        {
            foreach (var piece in stageData.mapPieces)
                if (piece.id == pieceId)
                    return piece.position.ToVector2() + localPos.ToVector2();
        }
        return localPos.ToVector2();
    }

    // ── 좌표 변환 ─────────────────────────────────────────────
    Vector2 WorldToViewport(Vector2 world, Rect rect)
    {
        // 6. 뷰포트 rect의 center + viewOffset이 (0,0) 월드
        Vector2 origin = rect.center + viewOffset;
        return new Vector2(origin.x + world.x * viewScale,
                           origin.y - world.y * viewScale);
    }

    Vector2 ViewportToWorld(Vector2 vp, Rect rect)
    {
        Vector2 origin = rect.center + viewOffset;
        return new Vector2((vp.x - origin.x) / viewScale,
                           -(vp.y - origin.y) / viewScale);
    }

    Rect WorldRectToViewport(Vector2 bl, Vector2 size, Rect vr)
    {
        Vector2 tl = WorldToViewport(bl + new Vector2(0, size.y), vr);
        Vector2 br = WorldToViewport(bl + new Vector2(size.x, 0), vr);
        return new Rect(tl.x, tl.y, Mathf.Max(1, br.x - tl.x), Mathf.Max(1, br.y - tl.y));
    }

    bool InRect(Vector2 p, Vector2 c, Vector2 s)
    {
        Vector2 h = s * 0.5f;
        return p.x >= c.x - h.x && p.x <= c.x + h.x && p.y >= c.y - h.y && p.y <= c.y + h.y;
    }

    // ── 스냅 헬퍼 ─────────────────────────────────────────────
    float SnapF(float v, float s) => s > 0 ? Mathf.Round(v / s) * s : v;
    Vector2 SnapV2(Vector2 v, float s) => new Vector2(SnapF(v.x, s), SnapF(v.y, s));

    // 레이블을 왼쪽에, X/Y 입력을 나머지 공간에 균등 배치
    SerializableVector2 DrawV2Field(string label, SerializableVector2 sv2, float snap)
    {
        Vector2 v = sv2.ToVector2();
        EditorGUILayout.BeginHorizontal();

        // 레이블
        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));

        // X: - [숫자] +
        EditorGUILayout.LabelField("X", GUILayout.Width(14));
        if (GUILayout.Button("-", GUILayout.Width(24))) v.x -= snap;
        v.x = SnapF(EditorGUILayout.FloatField(v.x), snap);
        if (GUILayout.Button("+", GUILayout.Width(24))) v.x += snap;

        EditorGUILayout.Space(8);

        // Y: - [숫자] +
        EditorGUILayout.LabelField("Y", GUILayout.Width(14));
        if (GUILayout.Button("-", GUILayout.Width(24))) v.y -= snap;
        v.y = SnapF(EditorGUILayout.FloatField(v.y), snap);
        if (GUILayout.Button("+", GUILayout.Width(24))) v.y += snap;

        EditorGUILayout.EndHorizontal();
        return SerializableVector2.FromVector2(v);
    }

    SerializableVector2 DrawV2FieldPos(string label, SerializableVector2 sv2, float snap)
    {
        Vector2 v = sv2.ToVector2();
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));

        // W: - [숫자] +
        EditorGUILayout.LabelField("W", GUILayout.Width(14));
        if (GUILayout.Button("-", GUILayout.Width(24))) v.x = Mathf.Max(snap, v.x - snap);
        v.x = Mathf.Max(snap, SnapF(EditorGUILayout.FloatField(v.x), snap));
        if (GUILayout.Button("+", GUILayout.Width(24))) v.x += snap;

        EditorGUILayout.Space(8);

        // H: - [숫자] +
        EditorGUILayout.LabelField("H", GUILayout.Width(14));
        if (GUILayout.Button("-", GUILayout.Width(24))) v.y = Mathf.Max(snap, v.y - snap);
        v.y = Mathf.Max(snap, SnapF(EditorGUILayout.FloatField(v.y), snap));
        if (GUILayout.Button("+", GUILayout.Width(24))) v.y += snap;

        EditorGUILayout.EndHorizontal();
        return SerializableVector2.FromVector2(v);
    }

    // ── 맵 조각 / 플랫폼 추가 ────────────────────────────────
    void AddMapPiece()
    {
        RecordUndo();

        stageData.mapPieces.Add(new MapPieceData
        {
            id = $"piece_{stageData.mapPieces.Count}",
            position = new SerializableVector2(0, 0),
            colliderSize = new SerializableVector2(5, 5),
            isMovable = true
        });
        selectedPieceIndex = stageData.mapPieces.Count - 1;
        selectedPlatformIndex = -1;
    }

    // ── 파일 I/O ──────────────────────────────────────────────
    void NewStage()
    {
        if (EditorUtility.DisplayDialog("New Stage", "현재 작업을 버리겠습니까?", "확인", "취소"))
        { stageData = new StageData(); selectedPieceIndex = -1; selectedPlatformIndex = -1; }
    }

    void SaveToFile()
    {
        if (!string.IsNullOrEmpty(lastSavedPath) && savePath == lastSavedPath)
        {
            WriteFile(savePath);
            return;
        }

        string fullPath = Path.GetFullPath(savePath);
        if (File.Exists(fullPath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "파일 덮어쓰기",
                $"이미 존재하는 파일입니다:\n{savePath}\n\n덮어쓰시겠습니까?",
                "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        WriteFile(savePath);
    }

    void WriteFile(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonUtility.ToJson(stageData, true));
        AssetDatabase.Refresh();
        lastSavedPath = path;
        EditorUtility.DisplayDialog("저장 완료", path, "확인");
    }

    void LoadFromFile()
    {
        string path = EditorUtility.OpenFilePanel("Load JSON", "Assets/StreamingAssets", "json");
        if (string.IsNullOrEmpty(path)) return;
        stageData = JsonUtility.FromJson<StageData>(File.ReadAllText(path));
        savePath = "Assets" + path.Substring(Application.dataPath.Length);
        lastSavedPath = savePath;
        selectedPieceIndex = -1; selectedPlatformIndex = -1;
        Repaint();
    }

    // ── 플레이 테스트 ─────────────────────────────────────────
    void PlayTest()
    {
        string tmpPath = Path.Combine(Application.streamingAssetsPath, "_playtest_tmp.json");
        if (!Directory.Exists(Application.streamingAssetsPath))
            Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(tmpPath, JsonUtility.ToJson(stageData, true));
        AssetDatabase.Refresh();
        PlayerPrefs.SetString("PlayTestStageFile", "_playtest_tmp.json");
        PlayerPrefs.Save();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(testScenePath);
        EditorApplication.isPlaying = true;
    }

    void DrawDivider()
    {
        EditorGUILayout.Space(3);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.5f, 0.5f, 0.5f, 0.3f));
        EditorGUILayout.Space(3);
    }

    // ── 단축키 ────────────────────────────────────────────────
    void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        bool ctrl = e.control || e.command;

        if (ctrl && e.keyCode == KeyCode.S)
        { SaveToFile(); e.Use(); return; }

        if (ctrl && !e.shift && e.keyCode == KeyCode.Z)
        { PerformUndo(); e.Use(); return; }

        if (ctrl && (e.keyCode == KeyCode.Y || (e.shift && e.keyCode == KeyCode.Z)))
        { PerformRedo(); e.Use(); return; }

        if (ctrl && e.keyCode == KeyCode.C)
        { PerformCopy(); e.Use(); return; }

        if (ctrl && e.keyCode == KeyCode.V)
        { PerformPaste(); e.Use(); return; }
    }

    // ── Undo / Redo ───────────────────────────────────────────
    void RecordUndo()
    {
        undoStack.Add(JsonUtility.ToJson(stageData));
        if (undoStack.Count > MAX_UNDO)
            undoStack.RemoveAt(0);
        redoStack.Clear();
    }

    void PerformUndo()
    {
        if (undoStack.Count == 0) return;
        redoStack.Add(JsonUtility.ToJson(stageData));
        stageData = JsonUtility.FromJson<StageData>(undoStack[undoStack.Count - 1]);
        undoStack.RemoveAt(undoStack.Count - 1);
        selectedPieceIndex = -1;
        selectedPlatformIndex = -1;
        Repaint();
    }

    void PerformRedo()
    {
        if (redoStack.Count == 0) return;
        undoStack.Add(JsonUtility.ToJson(stageData));
        stageData = JsonUtility.FromJson<StageData>(redoStack[redoStack.Count - 1]);
        redoStack.RemoveAt(redoStack.Count - 1);
        selectedPieceIndex = -1;
        selectedPlatformIndex = -1;
        Repaint();
    }

    // ── Copy / Paste ──────────────────────────────────────────
    void PerformCopy()
    {
        if (selectedPieceIndex >= 0 &&
            selectedPieceIndex < stageData.mapPieces.Count &&
            selectedPlatformIndex >= 1000)
        {
            int objIdx = selectedPlatformIndex - 1000;
            var piece = stageData.mapPieces[selectedPieceIndex];
            if (objIdx < piece.pieceObjects.Count)
            {
                copiedPieceObject = JsonUtility.FromJson<PieceObjectData>(
                    JsonUtility.ToJson(piece.pieceObjects[objIdx]));
                copiedPiece = null;
            }
            return;
        }

        if (selectedPieceIndex >= 0 && selectedPieceIndex < stageData.mapPieces.Count)
        {
            copiedPiece = JsonUtility.FromJson<MapPieceData>(
                JsonUtility.ToJson(stageData.mapPieces[selectedPieceIndex]));
            copiedPieceObject = null;
        }
    }

    void PerformPaste()
    {
        RecordUndo();

        if (copiedPieceObject != null &&
            selectedPieceIndex >= 0 &&
            selectedPieceIndex < stageData.mapPieces.Count)
        {
            var piece = stageData.mapPieces[selectedPieceIndex];
            var newObj = JsonUtility.FromJson<PieceObjectData>(
                JsonUtility.ToJson(copiedPieceObject));
            newObj.id = $"{newObj.id}_copy";
            newObj.localPosition = new SerializableVector2(
                newObj.localPosition.x + snapPosition,
                newObj.localPosition.y);
            piece.pieceObjects.Add(newObj);
            selectedPlatformIndex = 1000 + piece.pieceObjects.Count - 1;
            Repaint();
            return;
        }

        if (copiedPiece != null)
        {
            var newPiece = JsonUtility.FromJson<MapPieceData>(
                JsonUtility.ToJson(copiedPiece));
            newPiece.id = $"{newPiece.id}_copy";
            newPiece.position = new SerializableVector2(
                newPiece.position.x + snapPosition,
                newPiece.position.y);
            stageData.mapPieces.Add(newPiece);
            selectedPieceIndex = stageData.mapPieces.Count - 1;
            selectedPlatformIndex = -1;
            Repaint();
        }
    }
}