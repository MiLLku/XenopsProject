using System;
using UnityEngine;


[Serializable]
public struct StampElement
{
    // 스탬프의 (0,0)을 기준으로 한 상대적 위치
    public Vector2Int position;
    
    // 이 요소의 타입
    public TypeObjectTile type;
    
    // 타일 ID, 건물 ID, 적 ID 등 실제 배치될 리소스의 고유 ID
    public int id;
}
