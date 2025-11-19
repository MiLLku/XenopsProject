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



