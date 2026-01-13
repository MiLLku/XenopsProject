using System;

/// <summary>
/// 건물 저장 데이터 (완성된 건물)
/// </summary>
[Serializable]
public class BuildingSaveData
{
    public int instanceId;          // 런타임 고유 ID
    public int dataId;              // BuildingData ScriptableObject ID

    // 위치 (피벗 기준, 왼쪽 아래)
    public int gridX;
    public int gridY;

    // 상태
    public int currentHealth;
    public bool isFunctional;

    // 건물별 추가 데이터 (창고 내용물 등)
    // 복잡한 데이터는 JSON으로 직렬화하여 저장
    public string extraData;
}

/// <summary>
/// 건설 현장 저장 데이터 (건설 중인 건물)
/// </summary>
[Serializable]
public class ConstructionSiteSaveData
{
    public int instanceId;
    public int buildingDataId;      // 건설 중인 BuildingData ID

    // 위치 (피벗 기준)
    public int gridX;
    public int gridY;

    // 건설 상태
    public int state;               // ConstructionState enum
    public float progress;          // 0~1

    // 연결된 작업
    public int workOrderId;         // -1이면 없음

    public ConstructionSiteSaveData()
    {
        workOrderId = -1;
    }
}
