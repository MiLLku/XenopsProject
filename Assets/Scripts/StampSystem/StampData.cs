using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class StampData
{
    // 스탬프를 찾는 고유 키 (예: "Iron_Vein_Large", "BossDungeon_1")
    public string key;
    
    // 스탬프의 전체 크기 (정보용)
    public Vector2Int size;
    
    // 스탬프를 배치할 때의 기준점 (Pivot)
    // (0, 0) = 왼쪽 아래, (size.x/2, size.y/2) = 중앙
    public Vector2Int pivot;

    // 지형 타일 요소 목록
    public List<StampElement> elements;
    
    // 벽 타일 요소 목록
    public List<StampElement> wallElements;

    public StampData()
    {
        elements = new List<StampElement>();
        wallElements = new List<StampElement>();
    }
}
