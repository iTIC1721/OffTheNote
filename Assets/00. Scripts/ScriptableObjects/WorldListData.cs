using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 월드 목록 ScriptableObject
/// Create → Game → World List 로 생성
/// 씬에서 WorldSelectManager에 연결
/// </summary>
[CreateAssetMenu(fileName = "WorldList", menuName = "Game/World List")]
public class WorldListData : ScriptableObject
{
    public List<WorldData> worlds = new List<WorldData>();
}