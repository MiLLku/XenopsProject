using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 플레이어가 생성한 작업 명령 (작업물)
/// WorkTaskQueue를 통해 개별 작업(WorkTask)을 관리합니다.
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
    
    [Header("작업 큐")]
    public WorkTaskQueue taskQueue;       // 작업 큐 (개별 타일/대상별 작업 관리)
    
    [Header("인력 할당")]
    public int maxAssignedWorkers;        // 최대 할당 가능 작업자 수 (플레이어가 설정)
    public List<Employee> assignedWorkers; // 현재 할당된 작업자들 (작업물에 등록된 직원)
    
    [Header("진행 상태")]
    public bool isActive;                 // 활성화 상태
    public bool isPaused;                 // 일시정지 상태
    
    // 이벤트
    public delegate void WorkOrderDelegate(WorkOrder order);
    public event WorkOrderDelegate OnCompleted;
    public event WorkOrderDelegate OnCancelled;
    
    // ===== 기존 호환성을 위한 래퍼 프로퍼티 =====
    // (기존 코드에서 targets를 직접 참조하는 경우를 위해)
    
    /// <summary>
    /// [호환성] 아직 완료되지 않은 작업 대상 목록
    /// </summary>
    public List<IWorkTarget> targets 
    {
        get 
        {
            var result = new List<IWorkTarget>();
            foreach (var task in taskQueue.PendingTasks)
            {
                if (task.target != null) result.Add(task.target);
            }
            foreach (var task in taskQueue.AssignedTasks)
            {
                if (task.target != null) result.Add(task.target);
            }
            return result;
        }
    }
    
    /// <summary>
    /// [호환성] 완료된 작업 대상 목록
    /// </summary>
    public List<IWorkTarget> completedTargets
    {
        get
        {
            var result = new List<IWorkTarget>();
            foreach (var task in taskQueue.CompletedTasks)
            {
                if (task.target != null) result.Add(task.target);
            }
            return result;
        }
    }
    
    /// <summary>
    /// [호환성] 작업자별 할당된 작업 매핑
    /// </summary>
    public Dictionary<Employee, IWorkTarget> workerAssignments
    {
        get
        {
            var result = new Dictionary<Employee, IWorkTarget>();
            foreach (var task in taskQueue.AssignedTasks)
            {
                if (task.assignedWorker != null && task.target != null)
                {
                    result[task.assignedWorker] = task.target;
                }
            }
            return result;
        }
    }
    
    // ===== 생성자 =====
    
    public WorkOrder()
    {
        taskQueue = new WorkTaskQueue();
        assignedWorkers = new List<Employee>();
        
        // 큐 이벤트 연결
        taskQueue.OnAllTasksCompleted += OnQueueCompleted;
    }
    
    private void OnQueueCompleted(WorkTask task)
    {
        OnCompleted?.Invoke(this);
    }
    
    // ===== 작업 타입 관련 =====
    
    /// <summary>
    /// 작업 타입별 동시 작업 가능 여부
    /// </summary>
    public static bool CanMultipleWorkersWork(WorkType type)
    {
        switch (type)
        {
            case WorkType.Mining:
            case WorkType.Chopping:
            case WorkType.Gardening:
            case WorkType.Hauling:
            case WorkType.Demolish:
                return true;
                
            case WorkType.Crafting:
            case WorkType.Research:
            case WorkType.Building:
            case WorkType.Resting:
            case WorkType.Eating:
                return false;
                
            default:
                return false;
        }
    }
    
    // ===== 작업 대상(타겟) 관리 =====
    
    /// <summary>
    /// 작업 대상을 추가합니다. (WorkTask로 변환하여 큐에 추가)
    /// </summary>
    public void AddTarget(IWorkTarget target)
    {
        if (target == null) return;
        
        WorkTask task = new WorkTask(target, priority);
        taskQueue.Enqueue(task);
    }
    
    /// <summary>
    /// 여러 작업 대상을 한번에 추가합니다.
    /// </summary>
    public void AddTargets(List<IWorkTarget> newTargets)
    {
        if (newTargets == null) return;
        
        var tasks = new List<WorkTask>();
        foreach (var target in newTargets)
        {
            if (target != null)
            {
                tasks.Add(new WorkTask(target, priority));
            }
        }
        taskQueue.EnqueueRange(tasks);
    }
    
    /// <summary>
    /// [호환성] 아직 할당되지 않은 작업 대상을 반환합니다.
    /// </summary>
    public List<IWorkTarget> GetAvailableTargets()
    {
        return taskQueue.PendingTasks
            .Where(t => t.IsValid())
            .Select(t => t.target)
            .ToList();
    }
    
    // ===== 작업자 관리 =====
    
    /// <summary>
    /// 작업자를 할당할 수 있는지 확인합니다.
    /// </summary>
    public bool CanAssignWorker()
    {
        if (!isActive || isPaused) return false;
        if (!taskQueue.HasPendingTasks()) return false;
        if (assignedWorkers.Count >= maxAssignedWorkers) return false;
        
        if (!CanMultipleWorkersWork(workType) && assignedWorkers.Count > 0)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 작업자를 작업물에 등록합니다.
    /// (실제 작업 할당은 AssignNextTaskToWorker에서)
    /// </summary>
    public bool AssignWorker(Employee worker)
    {
        if (!CanAssignWorker() || worker == null)
            return false;
        
        if (assignedWorkers.Contains(worker))
            return true; // 이미 등록됨
        
        assignedWorkers.Add(worker);
        return true;
    }
    
    /// <summary>
    /// 작업자 등록을 해제합니다.
    /// </summary>
    public void UnassignWorker(Employee worker)
    {
        if (worker == null) return;
        
        assignedWorkers.Remove(worker);
        taskQueue.UnassignWorkerTasks(worker);
    }
    
    /// <summary>
    /// 특정 직원에게 다음 작업을 할당합니다.
    /// </summary>
    public WorkTask AssignNextTaskToWorker(Employee worker)
    {
        if (worker == null || !assignedWorkers.Contains(worker))
            return null;
        
        // 먼저 작업 범위 내의 작업 시도
        WorkTask task = taskQueue.AssignNextTaskInRange(worker);
        
        // 범위 내에 없으면 가장 가까운 작업 할당
        if (task == null)
        {
            task = taskQueue.AssignNextTask(worker);
        }
        
        return task;
    }
    
    /// <summary>
    /// [호환성] 특정 작업자가 작업할 대상을 할당합니다.
    /// </summary>
    public bool AssignTargetToWorker(Employee worker, IWorkTarget target)
    {
        if (worker == null || target == null) return false;
        
        // 해당 타겟을 가진 대기 중인 작업 찾기
        var task = taskQueue.PendingTasks.FirstOrDefault(t => t.target == target);
        
        if (task != null)
        {
            return task.Assign(worker);
        }
        
        return false;
    }
    
    /// <summary>
    /// 특정 직원이 이 작업물에서 작업 중인지 확인합니다.
    /// </summary>
    public bool IsWorkerAssigned(Employee worker)
    {
        return assignedWorkers.Contains(worker);
    }
    
    /// <summary>
    /// 특정 직원의 현재 작업을 반환합니다.
    /// </summary>
    public WorkTask GetWorkerCurrentTask(Employee worker)
    {
        return taskQueue.GetWorkerTask(worker);
    }
    
    // ===== 작업 완료 처리 =====
    
    /// <summary>
    /// [호환성] 특정 작업이 완료되었음을 표시합니다.
    /// </summary>
    public void CompleteTarget(IWorkTarget target, Employee worker)
    {
        if (target == null) return;
        
        // 해당 타겟을 가진 할당된 작업 찾기
        var task = taskQueue.AssignedTasks.FirstOrDefault(t => t.target == target);
        
        if (task != null)
        {
            taskQueue.CompleteTask(task);
        }
    }
    
    /// <summary>
    /// WorkTask 완료 처리
    /// </summary>
    public void CompleteTask(WorkTask task)
    {
        if (task == null) return;
        taskQueue.CompleteTask(task);
    }
    
    // ===== 상태 관리 =====
    
    /// <summary>
    /// 작업물이 완전히 완료되었는지 확인합니다.
    /// </summary>
    public bool IsCompleted()
    {
        return taskQueue.IsCompleted();
    }
    
    /// <summary>
    /// 진행률을 반환합니다 (0~1).
    /// </summary>
    public float GetProgress()
    {
        return taskQueue.GetProgress();
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
            taskQueue.UnassignWorkerTasks(worker);
            worker.CancelWork();
        }
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
        
        // 모든 작업 취소
        taskQueue.CancelAll();
        
        // 모든 작업자의 작업 취소
        foreach (var worker in assignedWorkers.ToList())
        {
            worker.CancelWork();
        }
        
        assignedWorkers.Clear();
        
        OnCancelled?.Invoke(this);
    }
    
    /// <summary>
    /// 유효하지 않은 작업 정리
    /// </summary>
    public void CleanupInvalidTasks()
    {
        taskQueue.CleanupInvalidTasks();
    }
    
    // ===== 디버그 =====
    
    /// <summary>
    /// 디버그 정보를 반환합니다.
    /// </summary>
    public string GetDebugInfo()
    {
        return $"[WorkOrder {orderId}] {orderName} | Type:{workType} | " +
               $"Priority:{priority} | Workers:{assignedWorkers.Count}/{maxAssignedWorkers} | " +
               $"Queue: Pending:{taskQueue.PendingCount} Assigned:{taskQueue.AssignedCount} Completed:{taskQueue.CompletedCount} | " +
               $"Progress:{GetProgress() * 100:F0}% | Active:{isActive} Paused:{isPaused}";
    }
}