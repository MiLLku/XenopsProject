using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 작업 큐 관리자
/// WorkOrder에 속한 모든 WorkTask를 큐로 관리합니다.
/// 표면 타일(접근 가능한 타일)을 우선 선택하여 위에서 아래로 채굴합니다.
/// </summary>
[System.Serializable]
public class WorkTaskQueue
{
    [Header("큐 상태")]
    [SerializeField] private List<WorkTask> pendingTasks = new List<WorkTask>();
    [SerializeField] private List<WorkTask> assignedTasks = new List<WorkTask>();
    [SerializeField] private List<WorkTask> completedTasks = new List<WorkTask>();
    
    [Header("설정")]
    [SerializeField] private bool useDistancePriority = true;
    [SerializeField] private float distanceWeight = 0.5f;
    
    // 이벤트
    public delegate void TaskDelegate(WorkTask task);
    public event TaskDelegate OnTaskCompleted;
    public event TaskDelegate OnTaskCancelled;
    public event TaskDelegate OnAllTasksCompleted;
    
    #region 큐 관리
    
    public void Enqueue(WorkTask task)
    {
        if (task == null) return;
        
        if (!pendingTasks.Contains(task))
        {
            pendingTasks.Add(task);
            SortPendingTasks();
        }
    }
    
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
    
    #endregion
    
    #region 작업 할당
    
    // MiningTaskSelector 캐시
    private MiningTaskSelector _miningSelector;
    private MiningTaskSelector MiningSelector
    {
        get
        {
            if (_miningSelector == null)
            {
                _miningSelector = new MiningTaskSelector();
            }
            return _miningSelector;
        }
    }
    
    /// <summary>
    /// 특정 직원에게 가장 적합한 다음 작업을 할당
    /// MiningTaskSelector를 사용하여 가중치 기반 선택
    /// </summary>
    public WorkTask AssignNextTask(Employee worker)
    {
        if (worker == null || pendingTasks.Count == 0)
            return null;
        
        var validTasks = pendingTasks.Where(t => t.IsValid()).ToList();
        
        if (validTasks.Count == 0)
            return null;
        
        // MiningTaskSelector로 최적 작업 선택
        WorkTask bestTask = MiningSelector.SelectBestTask(worker, validTasks);
        
        if (bestTask == null)
        {
            Debug.LogWarning("[WorkTaskQueue] 적합한 작업을 찾을 수 없습니다.");
            return null;
        }
        
        // 작업 할당
        if (bestTask.Assign(worker))
        {
            pendingTasks.Remove(bestTask);
            assignedTasks.Add(bestTask);
            
            Debug.Log($"[WorkTaskQueue] 작업 할당: {bestTask.GetPosition()}");
            return bestTask;
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
        
        var validTasks = pendingTasks.Where(t => t.IsValid()).ToList();
        
        if (validTasks.Count == 0)
            return null;
        
        // 직원 발 위치 (X는 반올림으로 시각적 위치와 맞춤)
        Vector3 workerPos = worker.transform.position;
        Vector2Int workerFootTile = new Vector2Int(
            Mathf.RoundToInt(workerPos.x),
            Mathf.FloorToInt(workerPos.y)
        );
        
        // 직원이 차지하는 타일들 (발, 몸통)
        HashSet<Vector2Int> workerOccupiedTiles = new HashSet<Vector2Int>
        {
            workerFootTile,
            new Vector2Int(workerFootTile.x, workerFootTile.y + 1)
        };
        
        // 작업 범위 내 + 차지하고 있지 않은 작업만 필터링
        var tasksInRange = validTasks.Where(task =>
        {
            Vector3 taskPos = task.GetPosition();
            Vector2Int taskTile = new Vector2Int(
                Mathf.FloorToInt(taskPos.x),
                Mathf.FloorToInt(taskPos.y)
            );
            Vector3Int taskTile3 = new Vector3Int(taskTile.x, taskTile.y, 0);
            
            // 직원이 차지하고 있는 타일 제외
            if (workerOccupiedTiles.Contains(taskTile))
                return false;
            
            // 작업 범위 내 + 시야 확보
            return worker.IsPositionInWorkRange(taskTile3);
        }).ToList();
        
        if (tasksInRange.Count == 0)
            return null;
        
        // MiningTaskSelector로 최적 선택
        WorkTask bestTask = MiningSelector.SelectBestTask(worker, tasksInRange);
        
        if (bestTask != null && bestTask.Assign(worker))
        {
            pendingTasks.Remove(bestTask);
            assignedTasks.Add(bestTask);
            return bestTask;
        }
        
        return null;
    }
    
    #endregion
    
    #region 헬퍼 메서드
    
    private void SortPendingTasks()
    {
        pendingTasks = pendingTasks
            .OrderBy(t => t.priority)
            .ThenBy(t => t.createdTime)
            .ToList();
    }
    
    #endregion
    
    #region 작업 완료/취소
    
    public void CompleteTask(WorkTask task)
    {
        if (task == null) return;
        
        task.Complete();
        
        if (assignedTasks.Contains(task))
        {
            assignedTasks.Remove(task);
        }
        else if (pendingTasks.Contains(task))
        {
            pendingTasks.Remove(task);
        }
        
        completedTasks.Add(task);
        
        OnTaskCompleted?.Invoke(task);
        
        if (pendingTasks.Count == 0 && assignedTasks.Count == 0)
        {
            OnAllTasksCompleted?.Invoke(task);
        }
    }
    
    public void CancelTask(WorkTask task)
    {
        if (task == null) return;
        
        task.Cancel();
        
        pendingTasks.Remove(task);
        assignedTasks.Remove(task);
        
        OnTaskCancelled?.Invoke(task);
    }
    
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
    
    public WorkTask GetWorkerTask(Employee worker)
    {
        return assignedTasks.FirstOrDefault(t => t.assignedWorker == worker);
    }
    
    public bool IsWorkerAssigned(Employee worker)
    {
        return assignedTasks.Any(t => t.assignedWorker == worker);
    }
    
    public void CleanupInvalidTasks()
    {
        pendingTasks.RemoveAll(t => !t.IsValid());
        
        var invalidAssigned = assignedTasks.Where(t => !t.IsValid()).ToList();
        foreach (var task in invalidAssigned)
        {
            CancelTask(task);
        }
    }
    
    public void CancelAll()
    {
        foreach (var task in assignedTasks.ToList())
        {
            task.Cancel();
        }
        assignedTasks.Clear();
        
        foreach (var task in pendingTasks.ToList())
        {
            task.Cancel();
        }
        pendingTasks.Clear();
    }
    
    #endregion
    
    #region 상태 확인
    
    public float GetProgress()
    {
        int total = pendingTasks.Count + assignedTasks.Count + completedTasks.Count;
        if (total == 0) return 1f;
        
        return (float)completedTasks.Count / total;
    }
    
    public bool IsCompleted()
    {
        return pendingTasks.Count == 0 && assignedTasks.Count == 0;
    }
    
    public bool HasPendingTasks()
    {
        return pendingTasks.Count > 0;
    }
    
    public bool HasTaskAtPosition(Vector3Int position)
    {
        Vector3 posFloat = new Vector3(position.x, position.y, 0);
        
        return pendingTasks.Any(t => Vector3.Distance(t.GetPosition(), posFloat) < 0.5f) ||
               assignedTasks.Any(t => Vector3.Distance(t.GetPosition(), posFloat) < 0.5f);
    }
    
    #endregion
    
    #region 프로퍼티
    
    public int PendingCount => pendingTasks.Count;
    public int AssignedCount => assignedTasks.Count;
    public int CompletedCount => completedTasks.Count;
    public int TotalCount => pendingTasks.Count + assignedTasks.Count + completedTasks.Count;
    
    public IReadOnlyList<WorkTask> PendingTasks => pendingTasks;
    public IReadOnlyList<WorkTask> AssignedTasks => assignedTasks;
    public IReadOnlyList<WorkTask> CompletedTasks => completedTasks;
    
    #endregion
}