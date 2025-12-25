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
    
    /// <summary>
    /// 특정 직원에게 가장 적합한 다음 작업을 할당
    /// 도달 가능한 작업만 선택합니다.
    /// </summary>
    public WorkTask AssignNextTask(Employee worker)
    {
        if (worker == null || pendingTasks.Count == 0)
            return null;
        
        Vector3 workerPos = worker.transform.position;
        
        var validTasks = pendingTasks.Where(t => t.IsValid()).ToList();
        
        if (validTasks.Count == 0)
            return null;
        
        // Pathfinder 준비
        TilePathfinder pathfinder = null;
        GameMap gameMap = null;
        if (MapGenerator.instance != null)
        {
            gameMap = MapGenerator.instance.GameMapInstance;
            if (gameMap != null)
            {
                pathfinder = new TilePathfinder(gameMap);
            }
        }
        
        if (pathfinder == null || gameMap == null)
            return null;
        
        // 직원의 발 위치
        Vector2Int workerFootTile = new Vector2Int(
            Mathf.FloorToInt(workerPos.x),
            Mathf.FloorToInt(workerPos.y) - 2
        );
        
        // 모든 작업 타일 위치 수집
        HashSet<Vector2Int> allTaskTiles = new HashSet<Vector2Int>();
        foreach (var task in validTasks)
        {
            Vector3 pos = task.GetPosition();
            allTaskTiles.Add(new Vector2Int(
                Mathf.FloorToInt(pos.x),
                Mathf.FloorToInt(pos.y)
            ));
        }
        
        // 후보 수집
        List<TaskCandidate> candidates = new List<TaskCandidate>();
        
        foreach (var task in validTasks)
        {
            Vector3 taskPos = task.GetPosition();
            Vector2Int taskTile = new Vector2Int(
                Mathf.FloorToInt(taskPos.x),
                Mathf.FloorToInt(taskPos.y)
            );
            Vector3Int taskTile3 = new Vector3Int(taskTile.x, taskTile.y, 0);
            
            // 1. 직원이 서 있는 타일인지 확인
            bool standingOnTask = (taskTile == workerFootTile);
            
            // 2. 작업 가능한 위치 찾기
            Vector2Int? workPosition = null;
            bool isInRange = false;
            
            if (standingOnTask)
            {
                // 직원이 이 타일 위에 서 있음 -> 옆으로 이동해야 함
                workPosition = FindAlternativeWorkPosition(
                    pathfinder, gameMap, workerFootTile, taskTile, allTaskTiles, worker);
                
                if (!workPosition.HasValue)
                {
                    // 이동할 곳이 없으면 스킵
                    continue;
                }
            }
            else
            {
                // IsPositionInWorkRange가 시야 체크까지 하므로, 진짜 작업 가능한지 확인
                isInRange = worker.IsPositionInWorkRange(taskTile3);
                
                if (!isInRange)
                {
                    // 범위 밖이면 이동해서 작업할 위치 찾기
                    workPosition = FindReachableWorkPosition(
                        pathfinder, workerFootTile, taskTile3, allTaskTiles, worker);
                    if (!workPosition.HasValue)
                    {
                        // 도달 불가능
                        continue;
                    }
                }
            }
            
            // 3. 유효한 후보!
            float distance = 0;
            if (workPosition.HasValue)
            {
                distance = Vector2Int.Distance(workerFootTile, workPosition.Value);
            }
            
            candidates.Add(new TaskCandidate
            {
                task = task,
                distance = distance,
                priority = task.priority,
                isInRange = isInRange && !standingOnTask,
                standingOnTask = standingOnTask
            });
        }
        
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[WorkTaskQueue] 접근 가능한 작업이 없습니다.");
            return null;
        }
        
        // 4. 후보 중 최적 선택
        var bestCandidate = candidates
            .OrderBy(c => c.standingOnTask ? 1 : 0)   // 서 있는 타일은 후순위
            .ThenByDescending(c => c.isInRange)        // 범위 내 우선
            .ThenBy(c => c.priority)                   // 우선순위 낮은 것
            .ThenBy(c => c.distance)                   // 거리 가까운 것
            .First();
        
        if (bestCandidate.task.Assign(worker))
        {
            pendingTasks.Remove(bestCandidate.task);
            assignedTasks.Add(bestCandidate.task);
            
            Debug.Log($"[WorkTaskQueue] 작업 할당: {bestCandidate.task.GetPosition()} " +
                     $"(후보 {candidates.Count}개, 범위내:{bestCandidate.isInRange}, 서있음:{bestCandidate.standingOnTask})");
            return bestCandidate.task;
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
        
        // 직원 발 위치
        Vector3 workerPos = worker.transform.position;
        Vector2Int workerFootTile = new Vector2Int(
            Mathf.FloorToInt(workerPos.x),
            Mathf.FloorToInt(workerPos.y) - 2
        );
        
        // 작업 범위 내 + 서 있지 않은 작업 찾기
        var tasksInRange = new List<WorkTask>();
        
        foreach (var task in validTasks)
        {
            Vector3 taskPos = task.GetPosition();
            Vector2Int taskTile = new Vector2Int(
                Mathf.FloorToInt(taskPos.x),
                Mathf.FloorToInt(taskPos.y)
            );
            Vector3Int taskTile3 = new Vector3Int(taskTile.x, taskTile.y, 0);
            
            // 서 있는 타일 제외
            if (taskTile == workerFootTile)
                continue;
            
            // IsPositionInWorkRange가 시야 체크까지 함
            if (worker.IsPositionInWorkRange(taskTile3))
            {
                tasksInRange.Add(task);
            }
        }
        
        var sortedTasks = tasksInRange.OrderBy(t => t.priority).ThenBy(t => t.createdTime);
        
        foreach (var task in sortedTasks)
        {
            if (task.Assign(worker))
            {
                pendingTasks.Remove(task);
                assignedTasks.Add(task);
                return task;
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region 헬퍼 메서드
    
    private struct TaskCandidate
    {
        public WorkTask task;
        public float distance;
        public int priority;
        public bool isInRange;
        public bool standingOnTask;
    }
    
    /// <summary>
    /// 직원이 현재 타일 위에 서 있을 때, 옆으로 이동해서 작업할 위치를 찾습니다.
    /// </summary>
    private Vector2Int? FindAlternativeWorkPosition(
        TilePathfinder pathfinder,
        GameMap gameMap,
        Vector2Int workerPos,
        Vector2Int targetTile,
        HashSet<Vector2Int> allTaskTiles,
        Employee worker)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -3; dy <= 1; dy++)
            {
                Vector2Int candidatePos = new Vector2Int(
                    targetTile.x + dx,
                    targetTile.y + dy
                );
                
                // 현재 위치는 제외
                if (candidatePos == workerPos)
                    continue;
                
                // 맵 범위 확인
                if (candidatePos.x < 0 || candidatePos.x >= GameMap.MAP_WIDTH ||
                    candidatePos.y < 0 || candidatePos.y >= GameMap.MAP_HEIGHT)
                    continue;
                
                // 작업 대상 타일 위면 제외
                if (allTaskTiles.Contains(candidatePos))
                    continue;
                
                // 해당 위치가 유효한 위치인지 확인 (서 있을 수 있는지)
                if (!pathfinder.IsValidPosition(candidatePos))
                    continue;
                
                // 해당 위치에서 타겟에 대해 시야가 확보되는지 확인
                Vector3Int candidatePos3 = new Vector3Int(candidatePos.x, candidatePos.y, 0);
                Vector3Int targetTile3 = new Vector3Int(targetTile.x, targetTile.y, 0);
                
                // 임시로 worker 위치를 이동시켜서 체크하기 어려우므로,
                // 간단히 거리 범위만 확인
                int workDx = Mathf.Abs(targetTile.x - candidatePos.x);
                int workDy = targetTile.y - candidatePos.y;
                if (workDx > 1 || workDy < -1 || workDy > 3)
                    continue;
                
                candidates.Add(candidatePos);
            }
        }
        
        // 가장 가까운 도달 가능한 위치 찾기
        foreach (var candidate in candidates.OrderBy(c => Vector2Int.Distance(workerPos, c)))
        {
            var path = pathfinder.FindPath(workerPos, candidate);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 작업 대상에 도달 가능한 작업 위치를 찾습니다.
    /// </summary>
    private Vector2Int? FindReachableWorkPosition(
        TilePathfinder pathfinder, 
        Vector2Int workerPos, 
        Vector3Int targetTilePos,
        HashSet<Vector2Int> allTaskTiles,
        Employee worker)
    {
        if (pathfinder == null) return null;
        
        List<Vector2Int> candidates = new List<Vector2Int>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -3; dy <= 1; dy++)
            {
                Vector2Int candidatePos = new Vector2Int(
                    targetTilePos.x + dx,
                    targetTilePos.y + dy
                );
                
                // 맵 범위 확인
                if (candidatePos.x < 0 || candidatePos.x >= GameMap.MAP_WIDTH ||
                    candidatePos.y < 0 || candidatePos.y >= GameMap.MAP_HEIGHT)
                    continue;
                
                // 작업 대상 타일 위면 제외
                if (allTaskTiles.Contains(candidatePos))
                    continue;
                
                // 해당 위치가 유효한 위치인지 확인
                if (!pathfinder.IsValidPosition(candidatePos))
                    continue;
                
                // 해당 위치에서 타겟까지 거리 범위 확인
                int workDx = Mathf.Abs(targetTilePos.x - candidatePos.x);
                int workDy = targetTilePos.y - candidatePos.y;
                if (workDx > 1 || workDy < -1 || workDy > 3)
                    continue;
                
                candidates.Add(candidatePos);
            }
        }
        
        if (candidates.Count == 0)
            return null;
        
        // 가장 가까운 도달 가능한 위치 찾기
        foreach (var candidate in candidates.OrderBy(c => Vector2Int.Distance(workerPos, c)))
        {
            if (candidate == workerPos)
                return candidate;
            
            var path = pathfinder.FindPath(workerPos, candidate);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }
        
        return null;
    }
    
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