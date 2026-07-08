using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드 데이터 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "WorldData", menuName = "Game/World Data")]
public class WorldData : ScriptableObject
{
    [Header("월드 정보")]
    public string worldId = "world_00";
    public string displayName = "World 0";
    public string displayDesc = "World 0 Desc";
    public string displayShortDesc = "Desc";
    public Sprite displayIcon;

    [Header("BGM")]
    public string bgm = "world0";

    [Header("스테이지 목록")]
    [Tooltip("StreamingAssets 기준 상대 경로. 예: world_00/stage_00.json")]
    public List<string> stageFiles = new List<string>();
}