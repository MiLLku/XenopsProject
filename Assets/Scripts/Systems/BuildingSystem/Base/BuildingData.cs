using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
[CreateAssetMenu(fileName = "NewBuildingData", menuName = "StampSystem/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("건물을 식별하는 고유 ID (예: 3001)")]
    public int buildingID;
        
    [Tooltip("UI에 표시될 건물의 이름")]
    public string buildingName;
        
    [Tooltip("UI에 표시될 건물 아이콘")]
    public Sprite icon;
        
    [Tooltip("빌드 메뉴 정렬용 카테고리")]
    public BuildingCategory category;
        
    [TextArea(3, 5)]
    [Tooltip("UI에 표시될 건물 설명")]
    public string description;

    [Header("설치 정보")]
    [Tooltip("건설이 완료되었을 때 생성될 실제 프리팹 (예: Chest_3x2.prefab)")]
    public GameObject buildingPrefab;
        
    [Tooltip("건물이 차지하는 타일 크기")]
    public Vector2Int size = Vector2Int.one;
        
    [Tooltip("건설 기준점(Pivot)")]
    public Vector2Int pivot = Vector2Int.zero;
        
    [Header("건설 비용")]
    [Tooltip("건설에 걸리는 시간(초)")]
    public float constructionTime = 5f;
        
    [Tooltip("건설에 필요한 자원 목록")]
    public List<ResourceCost> requiredResources;
        
    [Header("게임플레이 스탯")]
    [Tooltip("건물의 최대 체력")]
    public int maxHealth = 100;
    
    [Header("이동 설정")]
    [Tooltip("직원의 이동을 막는지 여부")]
    public bool blocksMovement = true;
        
    // (나중에 여기에 '전력 소모량', '작업 포인트' 등을 추가로 추천할 수 있습니다.)
}