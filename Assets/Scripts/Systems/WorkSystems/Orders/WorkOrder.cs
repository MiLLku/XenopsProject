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
    
    // 작업 타입별 동시 작업 가능 여부를 판단하는 정적 메서드
    public static bool CanMultipleWorkersWork(WorkType type)
    {
        switch (type)
        {
            case WorkType.Mining:      // 채광 - 여러 명 가능
            case WorkType.Chopping:    // 벌목 - 여러 명 가능
            case WorkType.Gardening:   // 수확 - 여러 명 가능
            case WorkType.Hauling:     // 운반 - 여러 명 가능
            case WorkType.Demolish:    // 철거 - 여러 명 가능 (큰 건물)
                return true;
                
            case WorkType.Crafting:    // 제작 - 1명만
            case WorkType.Research:    // 연구 - 1명만
            case WorkType.Building:    // 건설 - 1명만
            case WorkType.Resting:     // 휴식 - 개인 활동
            case WorkType.Eating:      // 식사 - 개인 활동
                return false;
                
            default:
                return false;
        }
    }
    
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
        if (newTargets == null) return;
        
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
        // 비활성화 또는 일시정지 상태면 불가
        if (!isActive || isPaused) return false;
        
        // 가용 작업 대상이 없으면 불가
        if (GetAvailableTargets().Count == 0) return false;
        
        // 최대 작업자 수 확인
        if (assignedWorkers.Count >= maxAssignedWorkers) return false;
        
        // 단일 작업자만 가능한 작업 타입인 경우
        if (!CanMultipleWorkersWork(workType) && assignedWorkers.Count > 0)
        {
            return false;
        }
        
        return true;
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
                                   t != null &&
                                   t.IsWorkAvailable()).ToList();
    }
    
    /// <summary>
    /// 특정 작업이 완료되었음을 표시합니다.
    /// </summary>
    public void CompleteTarget(IWorkTarget target, Employee worker)
    {
        if (target == null) return;
        
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
    
    /// <summary>
    /// 특정 직원이 이 작업물에서 작업 중인지 확인합니다.
    /// </summary>
    public bool IsWorkerAssigned(Employee worker)
    {
        return assignedWorkers.Contains(worker);
    }
    
    /// <summary>
    /// 디버그 정보를 반환합니다.
    /// </summary>
    public string GetDebugInfo()
    {
        return $"[WorkOrder {orderId}] {orderName} | Type:{workType} | " +
               $"Priority:{priority} | Workers:{assignedWorkers.Count}/{maxAssignedWorkers} | " +
               $"Progress:{GetProgress() * 100:F0}% ({completedTargets.Count}/{targets.Count + completedTargets.Count}) | " +
               $"Active:{isActive} Paused:{isPaused}";
    }
}


