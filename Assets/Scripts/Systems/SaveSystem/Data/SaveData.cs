using System;
using System.Collections.Generic;

/// <summary>
/// 게임 저장 데이터의 최상위 컨테이너
/// 모든 게임 상태를 담습니다.
/// </summary>
[Serializable]
public class SaveData
{
    // 메타 정보
    public int saveVersion = 1;
    public string saveName;
    public long saveTimestamp;      // Unix timestamp
    public float playTime;          // 총 플레이 시간(초)

    // 게임 데이터
    public MapSaveData map;
    public List<EmployeeSaveData> employees;
    public List<BuildingSaveData> buildings;
    public List<ConstructionSiteSaveData> constructionSites;
    public List<WorkOrderSaveData> workOrders;
    public InventorySaveData inventory;
    public List<DroppedItemSaveData> droppedItems;

    // 이벤트 시스템
    public EventSystemSaveData eventSystem;

    // ID 카운터 (로드 후 이어서 발급하기 위함)
    public int nextInstanceId;

    public SaveData()
    {
        employees = new List<EmployeeSaveData>();
        buildings = new List<BuildingSaveData>();
        constructionSites = new List<ConstructionSiteSaveData>();
        workOrders = new List<WorkOrderSaveData>();
        droppedItems = new List<DroppedItemSaveData>();
    }
}
