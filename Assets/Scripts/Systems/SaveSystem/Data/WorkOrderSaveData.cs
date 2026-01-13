using System;
using System.Collections.Generic;

/// <summary>
/// 작업물 저장 데이터
/// </summary>
[Serializable]
public class WorkOrderSaveData
{
    public int orderId;
    public string orderName;
    public int workType;            // WorkType enum
    public int priority;
    public float createdTime;

    public int maxWorkers;
    public List<int> assignedWorkerIds;

    public bool isActive;
    public bool isPaused;

    // 작업 대상들
    public List<WorkTaskSaveData> pendingTasks;
    public List<WorkTaskSaveData> assignedTasks;
    // completedTasks는 저장할 필요 없음 (이미 완료됨)

    public WorkOrderSaveData()
    {
        assignedWorkerIds = new List<int>();
        pendingTasks = new List<WorkTaskSaveData>();
        assignedTasks = new List<WorkTaskSaveData>();
    }
}

/// <summary>
/// 개별 작업 저장 데이터
/// </summary>
[Serializable]
public class WorkTaskSaveData
{
    public int taskId;
    public int state;               // TaskState enum
    public int priority;
    public int assignedWorkerId;    // -1이면 미할당

    public float createdTime;
    public float assignedTime;
    public float startedTime;

    // 작업 대상 정보
    public int targetType;          // WorkTargetType enum
    public string targetData;       // JSON 직렬화된 대상 데이터

    public WorkTaskSaveData()
    {
        assignedWorkerId = -1;
    }
}

/// <summary>
/// 작업 대상 타입
/// </summary>
public enum WorkTargetType
{
    Mining = 0,
    Harvest = 1,
    Build = 2,
    Demolish = 3,
    Haul = 4
}

// ========== 작업 대상별 데이터 ==========

[Serializable]
public class MiningTargetData
{
    public int tileX;
    public int tileY;
    public int tileId;
}

[Serializable]
public class HarvestTargetData
{
    public int entityX;
    public int entityY;
    public int entityType;
}

[Serializable]
public class BuildTargetData
{
    public int constructionSiteId;
}

[Serializable]
public class DemolishTargetData
{
    public int buildingInstanceId;
}

[Serializable]
public class HaulTargetData
{
    public int itemId;
    public int amount;
    public float fromX;
    public float fromY;
    public float toX;
    public float toY;
}
