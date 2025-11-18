using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 플레이어가 생성한 작업 명령 (작업물)
/// 여러 작업 대상을 묶어서 관리하고, 여러 직원을 할당할 수 있습니다.
/// </summary>
[System.Serializable]
public class WorkOrder
{
    [Header("작업 기본 정보")]
    public int orderId;                   // 고유 ID
    public string orderName;              // 작업물 이름 (플레이어가 지정)
    public WorkType workType;             // 작업 타입
    public int priority;                  // 우선순위 (낮을수록 우선)
    public float createdTime;             // 생성 시간
    
    [Header("작업 대상")]
    public List<IWorkTarget> targets;     // 작업 대상 목록 (채광할 타일들, 벌목할 나무들 등)
    public List<IWorkTarget> completedTargets; // 완료된 작업 대상
    
    [Header("인력 할당")]
    public int maxAssignedWorkers;        // 최대 할당 가능 작업자 수 (플레이어가 설정)
    public List<Employee> assignedWorkers; // 현재 할당된 작업자들
    public Dictionary<Employee, IWorkTarget> workerAssignments; // 각 작업자가 맡은 구체적인 작업
    
    [Header("진행 상태")]
    public bool isActive;                 // 활성화 상태
    public bool isPaused;                 // 일시정지 상태
    
    public WorkOrder()
    {
        targets = new List<IWorkTarget>();
        completedTargets = new List<IWorkTarget>();
        assignedWorkers = new List<Employee>();
        workerAssignments = new Dictionary<Employee, IWorkTarget>();
    }
    
    /// <summary>
    /// 작업 대상을 추가합니다.
    /// </summary>
    public void AddTarget(IWorkTarget target)
    {
        if (target != null && !targets.Contains(target))
        {
            targets.Add(target);
        }
    }
    
    /// <summary>
    /// 여러 작업 대상을 한번에 추가합니다.
    /// </summary>
    public void AddTargets(List<IWorkTarget> newTargets)
    {
        foreach (var target in newTargets)
        {
            AddTarget(target);
        }
    }
    
    /// <summary>
    /// 작업자를 할당할 수 있는지 확인합니다.
    /// </summary>
    public bool CanAssignWorker()
    {
        return assignedWorkers.Count < maxAssignedWorkers && 
               GetAvailableTargets().Count > 0 &&
               isActive && !isPaused;
    }
    
    /// <summary>
    /// 작업자를 할당합니다.
    /// </summary>
    public bool AssignWorker(Employee worker)
    {
        if (!CanAssignWorker() || assignedWorkers.Contains(worker))
            return false;
        
        assignedWorkers.Add(worker);
        return true;
    }
    
    /// <summary>
    /// 작업자 할당을 해제합니다.
    /// </summary>
    public void UnassignWorker(Employee worker)
    {
        if (assignedWorkers.Contains(worker))
        {
            assignedWorkers.Remove(worker);
        }
        
        if (workerAssignments.ContainsKey(worker))
        {
            workerAssignments.Remove(worker);
        }
    }
    
    /// <summary>
    /// 특정 작업자가 작업할 대상을 할당합니다.
    /// </summary>
    public bool AssignTargetToWorker(Employee worker, IWorkTarget target)
    {
        if (!assignedWorkers.Contains(worker) || !targets.Contains(target))
            return false;
        
        workerAssignments[worker] = target;
        return true;
    }
    
    /// <summary>
    /// 아직 할당되지 않은 작업 대상을 반환합니다.
    /// </summary>
    public List<IWorkTarget> GetAvailableTargets()
    {
        var assignedTargets = workerAssignments.Values.ToList();
        return targets.Where(t => !assignedTargets.Contains(t) && 
                                   !completedTargets.Contains(t) &&
                                   t.IsWorkAvailable()).ToList();
    }
    
    /// <summary>
    /// 특정 작업이 완료되었음을 표시합니다.
    /// </summary>
    public void CompleteTarget(IWorkTarget target, Employee worker)
    {
        if (targets.Contains(target))
        {
            targets.Remove(target);
            completedTargets.Add(target);
        }
        
        // 작업자의 할당 해제
        if (workerAssignments.ContainsKey(worker))
        {
            workerAssignments.Remove(worker);
        }
    }
    
    /// <summary>
    /// 작업물이 완전히 완료되었는지 확인합니다.
    /// </summary>
    public bool IsCompleted()
    {
        return targets.Count == 0 && workerAssignments.Count == 0;
    }
    
    /// <summary>
    /// 진행률을 반환합니다 (0~1).
    /// </summary>
    public float GetProgress()
    {
        int total = targets.Count + completedTargets.Count;
        if (total == 0) return 1f;
        
        return (float)completedTargets.Count / total;
    }
    
    /// <summary>
    /// 작업물을 일시정지합니다.
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        
        // 모든 작업자의 작업 취소
        foreach (var worker in assignedWorkers.ToList())
        {
            worker.CancelWork();
        }
        
        workerAssignments.Clear();
    }
    
    /// <summary>
    /// 작업물을 재개합니다.
    /// </summary>
    public void Resume()
    {
        isPaused = false;
    }
    
    /// <summary>
    /// 작업물을 취소합니다.
    /// </summary>
    public void Cancel()
    {
        isActive = false;
        
        // 모든 작업자의 작업 취소
        foreach (var worker in assignedWorkers.ToList())
        {
            worker.CancelWork();
        }
        
        assignedWorkers.Clear();
        workerAssignments.Clear();
    }
}
/// <summary>
/// 채광 작업 명령
/// </summary>
[System.Serializable]
public class MiningOrder : IWorkTarget
{
    public Vector3Int position;
    public int tileID;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => new Vector3(position.x + 0.5f, position.y + 0.5f, 0);
    public WorkType GetWorkType() => WorkType.Mining;
    public float GetWorkTime() => 3f;
    public bool IsWorkAvailable() => !completed;
    
    public void CompleteWork(Employee worker)
    {
        completed = true;
        assignedWorker = null;
        
        // 실제 채광 실행
        if (MapGenerator.instance != null)
        {
            GameMap gameMap = MapGenerator.instance.GameMapInstance;
            MapRenderer mapRenderer = MapGenerator.instance.MapRendererInstance;
            ResourceManager resourceManager = MapGenerator.instance.ResourceManagerInstance;
            
            // 드롭 아이템 생성
            GameObject dropPrefab = resourceManager.GetDropPrefab(tileID);
            if (dropPrefab != null)
            {
                Vector3 dropPos = GetWorkPosition();
                Object.Instantiate(dropPrefab, dropPos, Quaternion.identity);
            }
            
            // 타일 제거
            gameMap.SetTile(position.x, position.y, 0);
            gameMap.UnmarkTileOccupied(position.x, position.y);
            mapRenderer.UpdateTileVisual(position.x, position.y);
            
            Debug.Log($"[MiningOrder] 채광 완료: {position}");
        }
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}
/// <summary>
/// 수확 작업 명령 (나무, 식물 등)
/// </summary>
[System.Serializable]
public class HarvestOrder : IWorkTarget
{
    public IHarvestable target;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => target?.GetHarvestType() ?? WorkType.Gardening;
    public float GetWorkTime() => target?.GetHarvestTime() ?? 2f;
    public bool IsWorkAvailable() => !completed && target != null && target.CanHarvest();
    
    public void CompleteWork(Employee worker)
    {
        if (target != null)
        {
            target.Harvest();
        }
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}

/// <summary>
/// 철거 작업 명령
/// </summary>
[System.Serializable]
public class DemolishOrder : IWorkTarget
{
    public Building building;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => WorkType.Demolish;
    public float GetWorkTime() => 5f;
    public bool IsWorkAvailable() => !completed && building != null;
    
    public void CompleteWork(Employee worker)
    {
        if (building != null)
        {
            // 자원 일부 반환
            if (InventoryManager.instance != null && building.buildingData != null)
            {
                foreach (var cost in building.buildingData.requiredResources)
                {
                    int returnAmount = Mathf.Max(1, cost.amount / 2);
                    InventoryManager.instance.AddItem(cost.item, returnAmount);
                }
            }
            
            // 점유 해제
            if (MapGenerator.instance != null)
            {
                GameMap gameMap = MapGenerator.instance.GameMapInstance;
                Vector2Int cellPos = new Vector2Int(
                    Mathf.FloorToInt(building.transform.position.x),
                    Mathf.FloorToInt(building.transform.position.y)
                );
                
                for (int y = 0; y < building.buildingData.size.y; y++)
                {
                    for (int x = 0; x < building.buildingData.size.x; x++)
                    {
                        gameMap.UnmarkTileOccupied(cellPos.x + x, cellPos.y + y);
                    }
                }
            }
            
            Object.Destroy(building.gameObject);
            Debug.Log($"[DemolishOrder] 철거 완료: {building.buildingData.buildingName}");
        }
        
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}