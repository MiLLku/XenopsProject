using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 작업 큐 관리자
/// WorkOrder에 속한 모든 WorkTask를 큐로 관리합니다.
/// 우선순위와 거리 기반으로 다음 작업을 할당합니다.
/// </summary>
[System.Serializable]
public class WorkTaskQueue
{
    [Header("큐 상태")]
    [SerializeField] private List<WorkTask> pendingTasks = new List<WorkTask>();      // 대기 중인 작업
    [SerializeField] private List<WorkTask> assignedTasks = new List<WorkTask>();     // 할당된 작업
    [SerializeField] private List<WorkTask> completedTasks = new List<WorkTask>();    // 완료된 작업
    
    [Header("설정")]
    [SerializeField] private bool useDistancePriority = true;  // 거리 기반 우선순위 사용
    [SerializeField] private float distanceWeight = 0.5f;      // 거리 가중치 (0~1)
    
    // 이벤트
    public delegate void TaskDelegate(WorkTask task);
    public event TaskDelegate OnTaskCompleted;
    public event TaskDelegate OnTaskCancelled;
    public event TaskDelegate OnAllTasksCompleted;
    
    /// <summary>
    /// 새 작업을 큐에 추가
    /// </summary>
    public void Enqueue(WorkTask task)
    {
        if (task == null) return;
        
        if (!pendingTasks.Contains(task))
        {
            pendingTasks.Add(task);
            SortPendingTasks();
        }
    }
    
    /// <summary>
    /// 여러 작업을 한번에 추가
    /// </summary>
    public void EnqueueRange(IEnumerable<WorkTask> tasks)
    {
        foreach (var task in tasks)
        {
            if (task != null && !pendingTasks.Contains(task))
            {
                pendingTasks.Add(task);
            }
        }
        SortPendingTasks();
    }
    
    /// <summary>
    /// 특정 직원에게 가장 적합한 다음 작업을 할당
    /// </summary>
    public WorkTask AssignNextTask(Employee worker)
    {
        if (worker == null || pendingTasks.Count == 0)
            return null;
        
        // 직원의 작업 범위 내에 있는 작업 우선
        Vector3 workerPos = worker.transform.position;
        
        // 유효한 작업만 필터링
        var validTasks = pendingTasks.Where(t => t.IsValid()).ToList();
        
        if (validTasks.Count == 0)
            return null;
        
        WorkTask bestTask = null;
        float bestScore = float.MaxValue;
        
        foreach (var task in validTasks)
        {
            float score = CalculateTaskScore(task, worker, workerPos);
            
            if (score < bestScore)
            {
                bestScore = score;
                bestTask = task;
            }
        }
        
        if (bestTask != null)
        {
            // 작업 할당
            if (bestTask.Assign(worker))
            {
                pendingTasks.Remove(bestTask);
                assignedTasks.Add(bestTask);
                return bestTask;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 특정 직원의 작업 범위 내에서 가장 적합한 작업 할당
    /// </summary>
    public WorkTask AssignNextTaskInRange(Employee worker)
    {
        if (worker == null || pendingTasks.Count == 0)
            return null;
        
        // 유효한 작업만 필터링
        var validTasks = pendingTasks.Where(t => t.IsValid()).ToList();
        
        if (validTasks.Count == 0)
            return null;
        
        // 작업 범위 내의 작업 찾기
        foreach (var task in validTasks)
        {
            Vector3 taskPos = task.GetPosition();
            Vector3Int taskTilePos = new Vector3Int(
                Mathf.FloorToInt(taskPos.x),
                Mathf.FloorToInt(taskPos.y),
                0
            );
            
            if (worker.IsPositionInWorkRange(taskTilePos))
            {
                // 작업 할당
                if (task.Assign(worker))
                {
                    pendingTasks.Remove(task);
                    assignedTasks.Add(task);
                    return task;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 작업 점수 계산 (낮을수록 좋음)
    /// </summary>
    private float CalculateTaskScore(WorkTask task, Employee worker, Vector3 workerPos)
    {
        float priorityScore = task.priority;
        
        if (useDistancePriority)
        {
            Vector3 taskPos = task.GetPosition();
            float distance = Vector3.Distance(workerPos, taskPos);
            
            // 작업 범위 내에 있으면 보너스
            Vector3Int taskTilePos = new Vector3Int(
                Mathf.FloorToInt(taskPos.x),
                Mathf.FloorToInt(taskPos.y),
                0
            );
            
            if (worker.IsPositionInWorkRange(taskTilePos))
            {
                distance = 0; // 범위 내는 거리 0으로 취급
            }
            
            // 점수 = 우선순위 + (거리 * 가중치)
            return priorityScore + (distance * distanceWeight);
        }
        
        return priorityScore;
    }
    
    /// <summary>
    /// 작업 완료 처리
    /// </summary>
    public void CompleteTask(WorkTask task)
    {
        if (task == null) return;
        
        // 작업 완료
        task.Complete();
        
        // 리스트 이동
        if (assignedTasks.Contains(task))
        {
            assignedTasks.Remove(task);
        }
        else if (pendingTasks.Contains(task))
        {
            pendingTasks.Remove(task);
        }
        
        completedTasks.Add(task);
        
        // 이벤트 발생
        OnTaskCompleted?.Invoke(task);
        
        // 모든 작업 완료 확인
        if (pendingTasks.Count == 0 && assignedTasks.Count == 0)
        {
            OnAllTasksCompleted?.Invoke(task);
        }
    }
    
    /// <summary>
    /// 작업 취소 (큐에서 제거)
    /// </summary>
    public void CancelTask(WorkTask task)
    {
        if (task == null) return;
        
        task.Cancel();
        
        pendingTasks.Remove(task);
        assignedTasks.Remove(task);
        
        OnTaskCancelled?.Invoke(task);
    }
    
    /// <summary>
    /// 특정 직원의 작업 할당 해제 (작업은 다시 대기 상태로)
    /// </summary>
    public void UnassignWorkerTasks(Employee worker)
    {
        if (worker == null) return;
        
        var workerTasks = assignedTasks.Where(t => t.assignedWorker == worker).ToList();
        
        foreach (var task in workerTasks)
        {
            task.Unassign();
            assignedTasks.Remove(task);
            pendingTasks.Add(task);
        }
        
        SortPendingTasks();
    }
    
    /// <summary>
    /// 특정 직원에게 할당된 작업 반환
    /// </summary>
    public WorkTask GetWorkerTask(Employee worker)
    {
        return assignedTasks.FirstOrDefault(t => t.assignedWorker == worker);
    }
    
    /// <summary>
    /// 특정 직원이 이 큐에서 작업 중인지 확인
    /// </summary>
    public bool IsWorkerAssigned(Employee worker)
    {
        return assignedTasks.Any(t => t.assignedWorker == worker);
    }
    
    /// <summary>
    /// 대기 중인 작업 정렬
    /// </summary>
    private void SortPendingTasks()
    {
        pendingTasks = pendingTasks
            .OrderBy(t => t.priority)
            .ThenBy(t => t.createdTime)
            .ToList();
    }
    
    /// <summary>
    /// 유효하지 않은 작업 제거 (이미 파괴된 타일 등)
    /// </summary>
    public void CleanupInvalidTasks()
    {
        // 대기 중인 작업 중 유효하지 않은 것 제거
        pendingTasks.RemoveAll(t => !t.IsValid());
        
        // 할당된 작업 중 유효하지 않은 것 처리
        var invalidAssigned = assignedTasks.Where(t => !t.IsValid()).ToList();
        foreach (var task in invalidAssigned)
        {
            CancelTask(task);
        }
    }
    
    /// <summary>
    /// 모든 작업 취소
    /// </summary>
    public void CancelAll()
    {
        // 할당된 작업 취소
        foreach (var task in assignedTasks.ToList())
        {
            task.Cancel();
        }
        assignedTasks.Clear();
        
        // 대기 중인 작업 취소
        foreach (var task in pendingTasks.ToList())
        {
            task.Cancel();
        }
        pendingTasks.Clear();
    }
    
    /// <summary>
    /// 전체 진행률 반환 (0~1)
    /// </summary>
    public float GetProgress()
    {
        int total = pendingTasks.Count + assignedTasks.Count + completedTasks.Count;
        if (total == 0) return 1f;
        
        return (float)completedTasks.Count / total;
    }
    
    /// <summary>
    /// 완료 여부 확인
    /// </summary>
    public bool IsCompleted()
    {
        return pendingTasks.Count == 0 && assignedTasks.Count == 0;
    }
    
    /// <summary>
    /// 남은 작업이 있는지 확인
    /// </summary>
    public bool HasPendingTasks()
    {
        return pendingTasks.Count > 0;
    }
    
    /// <summary>
    /// 특정 위치의 작업이 있는지 확인
    /// </summary>
    public bool HasTaskAtPosition(Vector3Int position)
    {
        Vector3 posFloat = new Vector3(position.x, position.y, 0);
        
        return pendingTasks.Any(t => Vector3.Distance(t.GetPosition(), posFloat) < 0.5f) ||
               assignedTasks.Any(t => Vector3.Distance(t.GetPosition(), posFloat) < 0.5f);
    }
    
    // 읽기 전용 프로퍼티
    public int PendingCount => pendingTasks.Count;
    public int AssignedCount => assignedTasks.Count;
    public int CompletedCount => completedTasks.Count;
    public int TotalCount => pendingTasks.Count + assignedTasks.Count + completedTasks.Count;
    
    public IReadOnlyList<WorkTask> PendingTasks => pendingTasks;
    public IReadOnlyList<WorkTask> AssignedTasks => assignedTasks;
    public IReadOnlyList<WorkTask> CompletedTasks => completedTasks;
}