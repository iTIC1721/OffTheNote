using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StageLoader : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject mapPiecePrefab;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private GameObject movingPlatformPrefab;
    [SerializeField] private GameObject doubleJumpItemPrefab;
    [SerializeField] private GameObject blockerPrefab;
    [SerializeField] private GameObject hazardPrefab;
    [SerializeField] private GameObject movingHazardPrefab;
    [SerializeField] private GameObject laserEmitterPrefab;
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private GameObject goalPrefab;
    [SerializeField] private GameObject textObjectPrefab;

    [Header("Scene References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Camera mainCamera;

    [Header("Stage")]
    [SerializeField] private string stageFileName = "";

    private void Start()
    {
        if (string.IsNullOrEmpty(stageFileName)) return;
        LoadStage(stageFileName);
    }

    public void LoadStage(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path)) { Debug.LogError($"Stage file not found: {path}"); return; }
        LoadStageFromJson(File.ReadAllText(path));
    }

    public void LoadStageFromJson(string json)
    {
        BuildStage(JsonUtility.FromJson<StageData>(json));
    }

    void BuildStage(StageData data)
    {
        ClearStage();

        if (mainCamera != null)
            mainCamera.orthographicSize = data.cameraHeight * 0.5f;

        Dictionary<string, GameObject> pieceObjects = new Dictionary<string, GameObject>();

        // ── 맵 조각 생성 ──────────────────────────────────────
        foreach (var pieceData in data.mapPieces)
        {
            GameObject pieceObj = Instantiate(mapPiecePrefab);
            pieceObj.name = $"MapPiece_{pieceData.id}";
            pieceObj.transform.position = pieceData.position.ToVector2();

            MapPiece piece = pieceObj.GetComponent<MapPiece>();
            if (piece != null)
            {
                piece.SetMovable(pieceData.isVisible ? pieceData.isMovable : false);
                piece.SetVisible(pieceData.isVisible);

                if (pieceData.isPinned)
                    piece.SetupPin(true, pieceData.pinLocalPosition.ToVector2());
            }

            BoxCollider2D clickCol = pieceObj.GetComponent<BoxCollider2D>();
            if (clickCol != null) clickCol.size = pieceData.colliderSize.ToVector2();

            MapPieceBackground bg = pieceObj.GetComponent<MapPieceBackground>();
            if (bg != null) bg.FitToClickCollider();

            // 조각 종속 오브젝트 (Platform 포함)
            foreach (var objData in pieceData.pieceObjects)
                SpawnPieceObject(objData, pieceObj.transform);

            GameManager.Instance?.RegisterMapPiece(piece);
            pieceObjects[pieceData.id] = pieceObj;
        }

        // ── 스폰 포인트 ───────────────────────────────────────
        if (player != null)
        {
            GameObject spawnObj = Instantiate(spawnPrefab);
            spawnObj.name = "SpawnPoint";
            if (!string.IsNullOrEmpty(data.spawnPieceId) &&
                pieceObjects.TryGetValue(data.spawnPieceId, out GameObject spawnParent))
            {
                spawnObj.transform.SetParent(spawnParent.transform, false);
                spawnObj.transform.localPosition = data.spawnLocalPosition.ToVector2();
            }
            else
            {
                spawnObj.transform.position = data.spawnLocalPosition.ToVector2();
            }
            player.SetSpawnPoint(spawnObj.transform);
            player.Respawn(enableDeathEffect: false);
        }

        // ── 골 지점 ───────────────────────────────────────────
        if (goalPrefab != null)
        {
            GameObject goal = Instantiate(goalPrefab);
            goal.name = "GoalPoint";
            goal.tag = "Goal";
            if (!string.IsNullOrEmpty(data.goalPieceId) &&
                pieceObjects.TryGetValue(data.goalPieceId, out GameObject goalParent))
            {
                goal.transform.SetParent(goalParent.transform, false);
                goal.transform.localPosition = data.goalLocalPosition.ToVector2();
            }
            else
            {
                goal.transform.position = data.goalLocalPosition.ToVector2();
            }
        }

        // ── 글로벌 오브젝트 ───────────────────────────────────
        foreach (var objData in data.globalObjects)
            SpawnGlobalObject(objData);
    }

    void SpawnPieceObject(PieceObjectData data, Transform parent)
    {
        switch (data.type)
        {
            case PieceObjectType.Platform:
                if (platformPrefab == null) return;
                GameObject platObj = Instantiate(platformPrefab, parent);
                platObj.name = $"Platform_{data.id}";
                platObj.transform.localPosition = data.localPosition.ToVector2();
                platObj.transform.localScale = new Vector3(
                    data.platform.size.x, data.platform.size.y, 1f);
                break;

            case PieceObjectType.DoubleJumpItem:
                if (doubleJumpItemPrefab == null) return;
                GameObject item = Instantiate(doubleJumpItemPrefab, parent);
                item.name = $"DoubleJumpItem_{data.id}";
                item.transform.localPosition = data.localPosition.ToVector2();
                break;

            case PieceObjectType.MovingPlatform:
                if (movingPlatformPrefab == null) return;
                GameObject mp = Instantiate(movingPlatformPrefab, parent);
                mp.name = $"MovingPlatform_{data.id}";
                mp.transform.localPosition = data.localPosition.ToVector2();
                mp.GetComponent<MovingPlatform>()?.Initialize(data.movingPlatform);
                break;

            case PieceObjectType.Blocker:
                if (blockerPrefab == null) return;
                GameObject blocker = Instantiate(blockerPrefab, parent);
                blocker.name = $"Blocker_{data.id}";
                blocker.transform.localPosition = data.localPosition.ToVector2();
                blocker.transform.localScale = new Vector3(
                    data.blocker.size.x, data.blocker.size.y, 1f);
                break;

            case PieceObjectType.Hazard:
                if (hazardPrefab == null) return;
                GameObject hazard = Instantiate(hazardPrefab, parent);
                hazard.name = $"Hazard_{data.id}";
                hazard.transform.localPosition = data.localPosition.ToVector2();
                hazard.transform.localScale = new Vector3(
                    data.hazard.size.x, data.hazard.size.y, 1f);
                break;

            case PieceObjectType.MovingHazard:
                if (movingHazardPrefab == null) return;
                GameObject mh = Instantiate(movingHazardPrefab, parent);
                mh.name = $"MovingHazard_{data.id}";
                mh.transform.localPosition = data.localPosition.ToVector2();
                mh.GetComponent<MovingPlatform>()?.Initialize(data.movingPlatform);
                break;

            case PieceObjectType.LaserEmitter:
                if (laserEmitterPrefab == null) return;
                GameObject laser = Instantiate(laserEmitterPrefab, parent);
                laser.name = $"LaserEmitter_{data.id}";
                laser.transform.localPosition = data.localPosition.ToVector2();
                laser.transform.localScale = Vector3.one;       // 발사 장치는 1x1 고정
                laser.GetComponent<LaserEmitter>()?.Initialize(data.laserEmitter);
                break;
        }
    }

    void SpawnGlobalObject(GlobalObjectData data)
    {
        switch (data.type)
        {
            case GlobalObjectType.Text:
                if (textObjectPrefab == null) return;
                GameObject textObj = Instantiate(textObjectPrefab);
                textObj.name = $"Text_{data.id}";
                textObj.transform.position = data.position.ToVector2();

                // TextMeshPro 설정
                var tmp = textObj.GetComponentInChildren<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = data.text.content;
                    tmp.fontSize = data.text.fontSize;
                }
                break;

            case GlobalObjectType.Hazard:
                if (hazardPrefab == null) return;
                GameObject hazard = Instantiate(hazardPrefab);
                hazard.name = $"Hazard_{data.id}";
                hazard.transform.position = data.position.ToVector2();
                hazard.transform.localScale = new Vector3(
                    data.hazard.size.x, data.hazard.size.y, 1f);
                break;
        }
    }

    void ClearStage()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Goal")) Destroy(obj);
        foreach (var p in FindObjectsByType<MapPiece>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var t in FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
            if (t.transform.parent == null || t.gameObject.name.StartsWith("Text_"))
                Destroy(t.gameObject);
        GameManager.Instance?.ClearMapPieces();
    }
}