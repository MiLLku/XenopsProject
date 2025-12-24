using UnityEngine;

/// <summary>
/// 작업의 최소 단위 (타일 하나, 나무 하나 등)
/// WorkOrder에 속한 개별 작업을 나타냅니다.
/// </summary>
[System.Serializable]
public class WorkTask
{
    /// <summary>
    /// 작업 상태
    /// </summary>
    public enum TaskState
    {
        Pending,    // 대기 중 (큐에서 대기)
        Assigned,   // 할당됨 (직원에게 배정되었지만 아직 시작 안함)
        InProgress, // 진행 중 (직원이 작업 중)
        Completed,  // 완료됨
        Cancelled   // 취소됨
    }
    
    [Header("작업 정보")]
    public int taskId;                    // 고유 ID
    public IWorkTarget target;            // 실제 작업 대상 (MiningOrder, HarvestOrder 등)
    public TaskState state;               // 현재 상태
    public float createdTime;             // 생성 시간
    
    [Header("할당 정보")]
    public Employee assignedWorker;       // 할당된 직원 (null이면 미할당)
    public float assignedTime;            // 할당된 시간
    public float startedTime;             // 작업 시작 시간
    public float completedTime;           // 완료 시간
    
    [Header("우선순위")]
    public int priority;                  // 우선순위 (낮을수록 먼저)
    public float distanceFromWorker;      // 작업자로부터의 거리 (동적 계산용)
    
    // 정적 ID 카운터
    private static int nextTaskId = 1;
    
    /// <summary>
    /// 새 작업 생성
    /// </summary>
    public WorkTask(IWorkTarget workTarget, int taskPriority = 5)
    {
        taskId = nextTaskId++;
        target = workTarget;
        state = TaskState.Pending;
        priority = taskPriority;
        createdTime = Time.time;
        assignedWorker = null;
    }
    
    /// <summary>
    /// 작업을 직원에게 할당
    /// </summary>
    public bool Assign(Employee worker)
    {
        if (state != TaskState.Pending)
        {
            Debug.LogWarning($"[WorkTask] 작업 {taskId}는 Pending 상태가 아니라 할당할 수 없습니다. (현재: {state})");
            return false;
        }
        
        if (worker == null)
        {
            Debug.LogWarning($"[WorkTask] null 직원에게 할당할 수 없습니다.");
            return false;
        }
        
        assignedWorker = worker;
        state = TaskState.Assigned;
        assignedTime = Time.time;
        
        return true;
    }
    
    /// <summary>
    /// 작업 시작
    /// </summary>
    public bool Start()
    {
        if (state != TaskState.Assigned)
        {
            Debug.LogWarning($"[WorkTask] 작업 {taskId}는 Assigned 상태가 아니라 시작할 수 없습니다. (현재: {state})");
            return false;
        }
        
        state = TaskState.InProgress;
        startedTime = Time.time;
        
        return true;
    }
    
    /// <summary>
    /// 작업 완료
    /// </summary>
    public void Complete()
    {
        state = TaskState.Completed;
        completedTime = Time.time;
        
        // 실제 작업 완료 처리
        if (target != null && assignedWorker != null)
        {
            target.CompleteWork(assignedWorker);
        }
        
        // 직원 참조 해제
        assignedWorker = null;
    }
    
    /// <summary>
    /// 작업 취소 (할당 해제)
    /// </summary>
    public void Cancel()
    {
        if (state == TaskState.Completed)
        {
            Debug.LogWarning($"[WorkTask] 이미 완료된 작업 {taskId}는 취소할 수 없습니다.");
            return;
        }
        
        // 진행 중이었다면 작업 대상에게 알림
        if (target != null && assignedWorker != null)
        {
            target.CancelWork(assignedWorker);
        }
        
        state = TaskState.Cancelled;
        assignedWorker = null;
    }
    
    /// <summary>
    /// 할당 해제 (대기 상태로 되돌림)
    /// </summary>
    public void Unassign()
    {
        if (state == TaskState.Completed || state == TaskState.Cancelled)
        {
            Debug.LogWarning($"[WorkTask] 완료/취소된 작업 {taskId}는 할당 해제할 수 없습니다.");
            return;
        }
        
        // 작업 대상에게 알림
        if (target != null && assignedWorker != null)
        {
            target.CancelWork(assignedWorker);
        }
        
        assignedWorker = null;
        state = TaskState.Pending;
    }
    
    /// <summary>
    /// 작업이 유효한지 확인 (대상이 아직 작업 가능한지)
    /// </summary>
    public bool IsValid()
    {
        if (target == null) return false;
        if (state == TaskState.Completed || state == TaskState.Cancelled) return false;
        
        return target.IsWorkAvailable();
    }
    
    /// <summary>
    /// 작업 위치 반환
    /// </summary>
    public Vector3 GetPosition()
    {
        return target?.GetWorkPosition() ?? Vector3.zero;
    }
    
    /// <summary>
    /// 작업 타입 반환
    /// </summary>
    public WorkType GetWorkType()
    {
        return target?.GetWorkType() ?? WorkType.None;
    }
    
    /// <summary>
    /// 작업 시간 반환
    /// </summary>
    public float GetWorkTime()
    {
        return target?.GetWorkTime() ?? 0f;
    }
    
    /// <summary>
    /// 디버그 정보
    /// </summary>
    public override string ToString()
    {
        string workerName = assignedWorker != null ? assignedWorker.Data.employeeName : "없음";
        return $"[Task {taskId}] State:{state} | Worker:{workerName} | Priority:{priority} | Pos:{GetPosition()}";
    }
}