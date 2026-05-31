using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageData
{
    public string stageName = "New Stage";
    public float cameraHeight = 9f;

    public string spawnPieceId = "";
    public SerializableVector2 spawnLocalPosition = new SerializableVector2(0, 0);

    public string goalPieceId = "";
    public SerializableVector2 goalLocalPosition = new SerializableVector2(0, 0);

    public List<MapPieceData> mapPieces = new List<MapPieceData>();
    public List<GlobalObjectData> globalObjects = new List<GlobalObjectData>();
}

// 쪽지 동작 타입 — 에디터 드롭다운용 (JSON에는 별도 필드로 저장되지 않고 isMovable/isPinned/isFlippable 조합으로 직렬화)
public enum MapPieceType
{
    Fixed,    // isMovable=false, isPinned=false, isFlippable=false
    Movable,  // isMovable=true,  isPinned=false, isFlippable=false
    Pinned,   // isMovable=false, isPinned=true,  isFlippable=false
    Flip,     // isMovable=false, isPinned=false, isFlippable=true
}

[Serializable]
public class MapPieceData
{
    public string id = "";
    public SerializableVector2 position = new SerializableVector2(0, 0);
    public SerializableVector2 colliderSize = new SerializableVector2(5, 5);
    public bool isMovable = true; 
    public bool isVisible = true;

    public bool isPinned = false;
    public SerializableVector2 pinLocalPosition = new SerializableVector2(0, 0);

    public bool isFlippable = false;
    public string flipAxis = "Y";     // "X" (상하반전) or "Y" (좌우반전)
    public bool startFlipped = false;

    public List<PieceObjectData> pieceObjects = new List<PieceObjectData>(); // Platform 포함 모든 조각 종속 오브젝트

    // ── 에디터 전용 헬퍼 (JSON 직렬화와 무관) ──────────────────
    /// <summary>현재 필드 상태에서 MapPieceType을 추론</summary>
    public MapPieceType GetPieceType()
    {
        if (isFlippable) return MapPieceType.Flip;
        if (isPinned) return MapPieceType.Pinned;
        if (isMovable) return MapPieceType.Movable;
        return MapPieceType.Fixed;
    }

    /// <summary>MapPieceType을 적용해 관련 필드를 일괄 설정 (isVisible은 유지)</summary>
    public void SetPieceType(MapPieceType t)
    {
        isMovable = t == MapPieceType.Movable;
        isPinned = t == MapPieceType.Pinned;
        isFlippable = t == MapPieceType.Flip;
    }
}

// ── 오브젝트 타입 열거형 ──────────────────────────────────────
public enum PieceObjectType
{
    Platform,
    MovingPlatform,
    DoubleJumpItem,
    Blocker,
    Hazard,
    MovingHazard,
    LaserEmitter,
}

public enum GlobalObjectType
{
    Text,
    Hazard,
}

// ── 조각 종속 오브젝트 ────────────────────────────────────────
[Serializable]
public class PieceObjectData
{
    public string id = "";
    public PieceObjectType type = PieceObjectType.Platform;
    public SerializableVector2 localPosition = new SerializableVector2(0, 0);

    public PlatformData platform = new PlatformData();
    public MovingPlatformData movingPlatform = new MovingPlatformData();
    public BlockerData blocker = new BlockerData();
    public HazardData hazard = new HazardData();
    public LaserEmitterData laserEmitter = new LaserEmitterData();
}

// ── 씬 독립 오브젝트 ──────────────────────────────────────────
[Serializable]
public class GlobalObjectData
{
    public string id = "";
    public GlobalObjectType type = GlobalObjectType.Text;
    public SerializableVector2 position = new SerializableVector2(0, 0);

    public TextObjectData text = new TextObjectData();
    public HazardData hazard = new HazardData();
}

// ── Platform 데이터 ───────────────────────────────────────────
[Serializable]
public class PlatformData
{
    public SerializableVector2 size = new SerializableVector2(3, 0.5f);
}

// ── MovingPlatform 데이터 ─────────────────────────────────────
[Serializable]
public class MovingPlatformData
{
    public SerializableVector2 size = new SerializableVector2(3, 0.5f);
    public MovingPlatformMode mode = MovingPlatformMode.PingPong;
    public SerializableVector2 pointA = new SerializableVector2(0, 0);
    public SerializableVector2 pointB = new SerializableVector2(3, 0);
    public List<SerializableVector2> loopPoints = new List<SerializableVector2>();
    public float speed = 2f;
    public float waitTime = 0f;
}

public enum MovingPlatformMode
{
    PingPong,
    Loop,
}

// ── Blocker 데이터 ────────────────────────────────────────────
[Serializable]
public class BlockerData
{
    public SerializableVector2 size = new SerializableVector2(3, 0.5f);
}

// ── Hazard 데이터 ─────────────────────────────────────────
[Serializable]
public class HazardData
{
    public SerializableVector2 size = new SerializableVector2(3, 0.5f);
}

// ── LaserEmitter 데이터 ───────────────────────────────────
[Serializable]
public class LaserEmitterData
{
    public LaserDirection direction = LaserDirection.Right;
}

// ── Text 오브젝트 데이터 ──────────────────────────────────────
[Serializable]
public class TextObjectData
{
    public string content = "Text";
    public float fontSize = 1f;
}

// ── 공통 Vector2 직렬화 ───────────────────────────────────────
[Serializable]
public class SerializableVector2
{
    public float x, y;
    public SerializableVector2() { x = 0; y = 0; }
    public SerializableVector2(float x, float y) { this.x = x; this.y = y; }
    public Vector2 ToVector2() => new Vector2(x, y);
    public static SerializableVector2 FromVector2(Vector2 v) => new SerializableVector2(v.x, v.y);
}