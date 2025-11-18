using UnityEngine;

/// <summary>
/// 타일별 이동 속도 배율과 통과 가능 여부를 정의합니다.
/// </summary>
[System.Serializable]
public class TileMovementData
{
    [Header("타일 ID별 이동 속도 배율")]
    [Tooltip("AIR - 공중 이동 불가")]
    public float airSpeedMultiplier = 0f;
    
    [Tooltip("DIRT - 기본 속도")]
    public float dirtSpeedMultiplier = 1f;
    
    [Tooltip("STONE - 기본 속도")]
    public float stoneSpeedMultiplier = 1f;
    
    [Tooltip("COPPER_ORE - 기본 속도")]
    public float copperSpeedMultiplier = 1f;
    
    [Tooltip("IRON_ORE - 기본 속도")]
    public float ironSpeedMultiplier = 1f;
    
    [Tooltip("GOLD_ORE - 기본 속도")]
    public float goldSpeedMultiplier = 1f;
    
    [Tooltip("GRASS - 기본 속도")]
    public float grassSpeedMultiplier = 1f;
    
    [Tooltip("PROCESSED_DIRT - 평탄화된 땅, 빠른 이동")]
    public float processedDirtSpeedMultiplier = 1.2f;
    
    [Tooltip("LADDER - 사다리, 층간 이동용")]
    public float ladderSpeedMultiplier = 0.8f;
    
    /// <summary>
    /// 타일 ID에 따른 이동 속도 배율을 반환합니다.
    /// </summary>
    public float GetSpeedMultiplier(int tileId)
    {
        switch (tileId)
        {
            case 0: return airSpeedMultiplier;
            case 1: return dirtSpeedMultiplier;
            case 2: return stoneSpeedMultiplier;
            case 3: return copperSpeedMultiplier;
            case 4: return ironSpeedMultiplier;
            case 5: return goldSpeedMultiplier;
            case 6: return grassSpeedMultiplier;
            case 7: return processedDirtSpeedMultiplier;
            case 8: return ladderSpeedMultiplier;
            default: return 1f;
        }
    }
    
    /// <summary>
    /// 타일을 통과할 수 있는지 확인
    /// </summary>
    public bool IsPassable(int tileId)
    {
        // AIR와 LADDER는 통과 가능
        return tileId == 0 || tileId == 8;
    }
    
    /// <summary>
    /// 타일 위를 걸을 수 있는지 확인
    /// </summary>
    public bool IsWalkable(int tileId)
    {
        // AIR를 제외한 모든 타일은 발판이 될 수 있음
        return tileId != 0;
    }
    
    /// <summary>
    /// 사다리 타일인지 확인
    /// </summary>
    public bool IsLadder(int tileId)
    {
        return tileId == 8;
    }
}