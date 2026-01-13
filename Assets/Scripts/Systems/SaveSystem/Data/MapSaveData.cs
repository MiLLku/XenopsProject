using System;
using System.Collections.Generic;

/// <summary>
/// 맵 저장 데이터
/// 타일 그리드와 맵 엔티티(나무, 광물 등)를 저장합니다.
/// </summary>
[Serializable]
public class MapSaveData
{
    public int width;
    public int height;

    // 2D 배열을 1D로 저장 (인덱스 = y * width + x)
    public int[] tileGrid;
    public int[] wallGrid;

    // 맵 엔티티 (나무, 광물 등 자연물)
    public List<MapEntitySaveData> entities;

    public MapSaveData()
    {
        entities = new List<MapEntitySaveData>();
    }
}

/// <summary>
/// 맵 엔티티 저장 데이터 (나무, 광물 등)
/// </summary>
[Serializable]
public class MapEntitySaveData
{
    public int x;
    public int y;
    public int entityType;          // TypeObjectTile enum 값
    public int variantId;           // 변형 ID (나무 종류 등)
    public float remainingResource; // 남은 자원량
}
