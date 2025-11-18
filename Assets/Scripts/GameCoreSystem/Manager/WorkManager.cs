
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorkManager : DestroySingleton<WorkManager>
{
    [Header("작업 대기열")]
    [SerializeField] private List<WorkTask> pendingTasks = new List<WorkTask>();
    [SerializeField] private List<WorkTask> activeTasks = new List<WorkTask>();
    
    [Header("직원 관리")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    [SerializeField] private List<Employee> idleEmployees = new List<Employee>();
    
    [Header("작업 우선순위 설정")]
    [SerializeField] private WorkPrioritySettings prioritySettings;
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showWorkMarkers = true;
    
    private Dictionary<WorkType, Queue<WorkTask>> taskQueues;
    private Dictionary<IWorkTarget, WorkTask> targetToTaskMap;
    
    protected override void Awake()
    {
        base.Awake();
        InitializeQueues();
        targetToTaskMap = new Dictionary<IWorkTarget, WorkTask>();
        
        if (prioritySettings == null)
        {
            prioritySettings = new WorkPrioritySettings();
        }
    }
    
    void Start()
    {
        RefreshEmployeeList();
        InvokeRepeating(nameof(ProcessWorkQueue), 1f, 0.5f); // 0.5초마다 작업 처리
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
    
    private void InitializeQueues()
    {
        taskQueues = new Dictionary<WorkType, Queue<WorkTask>>();
        
        foreach (WorkType type in System.Enum.GetValues(typeof(WorkType)))
        {
            taskQueues[type] = new Queue<WorkTask>();
        }
    }
    
    #region 직원 관리
    
    public void RefreshEmployeeList()
    {
        // 이전 이벤트 구독 해제
        foreach (var emp in allEmployees)
        {
            if (emp != null)
            {
                emp.OnStateChanged -= OnEmployeeStateChanged;
            }
        }
        
        allEmployees.Clear();
        allEmployees.AddRange(FindObjectsOfType<Employee>());
        
        foreach (var emp in allEmployees)
        {
            emp.OnStateChanged += OnEmployeeStateChanged;
        }
        
        UpdateIdleEmployees();
        
        Debug.Log($"[WorkManager] {allEmployees.Count}명의 직원 등록 완료");
    }
    
    private void OnEmployeeStateChanged(EmployeeState state)
    {
        UpdateIdleEmployees();
        
        // 직원이 대기 상태가 되면 즉시 작업 할당 시도
        if (state == EmployeeState.Idle)
        {
            ProcessWorkQueue();
        }
    }
    
    private void UpdateIdleEmployees()
    {
        idleEmployees = allEmployees
            .Where(e => e != null && e.State == EmployeeState.Idle)
            .ToList();
    }
    
    public void RegisterEmployee(Employee employee)
    {
        if (!allEmployees.Contains(employee))
        {
            allEmployees.Add(employee);
            employee.OnStateChanged += OnEmployeeStateChanged;
            UpdateIdleEmployees();
        }
    }
    
    public void UnregisterEmployee(Employee employee)
    {
        if (allEmployees.Contains(employee))
        {
            allEmployees.Remove(employee);
            employee.OnStateChanged -= OnEmployeeStateChanged;
            UpdateIdleEmployees();
        }
    }
    
    #endregion
    
    #region 작업 등록
    
    public void RegisterWork(IWorkTarget target)
    {
        if (target == null) return;
        
        // 이미 등록된 작업인지 확인
        if (targetToTaskMap.ContainsKey(target)) return;
        
        WorkTask task = new WorkTask
        {
            target = target,
            type = target.GetWorkType(),
            position = target.GetWorkPosition(),
            priority = prioritySettings.GetPriority(target.GetWorkType()),
            createdTime = Time.time
        };
        
        AddTask(task);
    }
    
    private void AddTask(WorkTask task)
    {
        pendingTasks.Add(task);
        taskQueues[task.type].Enqueue(task);
        targetToTaskMap[task.target] = task;
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 새 작업 등록: {task.type} at {task.position}");
        }
        
        // 작업 마커 표시
        if (showWorkMarkers)
        {
            ShowWorkMarker(task);
        }
        
        // 즉시 할당 시도
        ProcessWorkQueue();
    }
    
    public void UnregisterWork(IWorkTarget target)
    {
        if (!targetToTaskMap.ContainsKey(target)) return;
        
        WorkTask task = targetToTaskMap[target];
        
        // 대기 중인 작업에서 제거
        if (pendingTasks.Contains(task))
        {
            pendingTasks.Remove(task);
        }
        
        // 진행 중인 작업에서 제거
        if (activeTasks.Contains(task))
        {
            task.assignedWorker?.CancelWork();
            activeTasks.Remove(task);
        }
        
        targetToTaskMap.Remove(target);
    }
    
    #endregion
    
    #region 작업 할당
    
    private void ProcessWorkQueue()
    {
        if (idleEmployees.Count == 0 || pendingTasks.Count == 0) return;
        
        // 우선순위별로 작업 정렬
        var sortedTasks = pendingTasks
            .OrderBy(t => t.priority)
            .ThenBy(t => t.createdTime)
            .ToList();
        
        foreach (var task in sortedTasks)
        {
            if (task.assignedWorker != null) continue;
            
            // 이 작업을 수행할 수 있는 가장 적합한 직원 찾기
            Employee bestWorker = FindBestWorkerForTask(task);
            
            if (bestWorker != null)
            {
                AssignTaskToEmployee(task, bestWorker);
                
                // 직원이 할당되면 다시 대기 직원 목록 업데이트
                UpdateIdleEmployees();
                
                if (idleEmployees.Count == 0) break;
            }
        }
    }
    
    private Employee FindBestWorkerForTask(WorkTask task)
    {
        var capableWorkers = idleEmployees
            .Where(e => e.CanPerformWork(task.type))
            .ToList();
        
        if (capableWorkers.Count == 0) return null;
        
        // 점수 기반으로 최적 직원 선택
        Employee bestWorker = null;
        float bestScore = float.MinValue;
        
        foreach (var worker in capableWorkers)
        {
            float score = CalculateWorkerTaskScore(worker, task);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestWorker = worker;
            }
        }
        
        return bestWorker;
    }
    
    private float CalculateWorkerTaskScore(Employee worker, WorkTask task)
    {
        float score = 0f;
        
        // 거리 (가까울수록 높은 점수)
        float distance = Vector3.Distance(worker.transform.position, task.position);
        score += (50f - distance) * 2f;
        
        // 작업 속도 (빠를수록 높은 점수)
        float workSpeed = worker.GetWorkSpeed(task.type);
        score += workSpeed * 30f;
        
        // 직원 상태 (체력, 정신력 고려)
        float healthRatio = worker.Stats.health / worker.Stats.maxHealth;
        float mentalRatio = worker.Stats.mental / worker.Stats.maxMental;
        score += (healthRatio + mentalRatio) * 10f;
        
        // 피로도 고려 (피로가 적을수록 높은 점수)
        float fatigueRatio = worker.Needs.fatigue / 100f;
        score += fatigueRatio * 20f;
        
        return score;
    }
    
    private void AssignTaskToEmployee(WorkTask task, Employee employee)
    {
        task.assignedWorker = employee;
        task.startTime = Time.time;
        
        pendingTasks.Remove(task);
        activeTasks.Add(task);
        idleEmployees.Remove(employee);
        
        employee.AssignWork(task.target);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {employee.Data.employeeName}에게 {task.type} 작업 할당 " +
                     $"(위치: {task.position})");
        }
    }
    
    #endregion
    
    #region 작업 완료 처리
    
    public void CompleteTask(IWorkTarget target)
    {
        if (!targetToTaskMap.ContainsKey(target)) return;
        
        WorkTask task = targetToTaskMap[target];
        
        if (activeTasks.Contains(task))
        {
            activeTasks.Remove(task);
        }
        
        targetToTaskMap.Remove(target);
        
        if (showDebugInfo)
        {
            float completionTime = Time.time - task.startTime;
            Debug.Log($"[WorkManager] 작업 완료: {task.type} " +
                     $"(소요 시간: {completionTime:F1}초)");
        }
        
        // 작업 마커 제거
        RemoveWorkMarker(task);
    }
    
    #endregion
    
    #region 유틸리티
    
    public IWorkTarget GetAvailableWork(WorkType type, Vector3 position, float radius)
    {
        var tasksOfType = pendingTasks
            .Where(t => t.type == type && t.assignedWorker == null)
            .Where(t => Vector3.Distance(t.position, position) <= radius)
            .OrderBy(t => t.priority)
            .ThenBy(t => Vector3.Distance(t.position, position));
        
        return tasksOfType.FirstOrDefault()?.target;
    }
    
    public List<WorkTask> GetPendingTasksOfType(WorkType type)
    {
        return pendingTasks.Where(t => t.type == type).ToList();
    }
    
    public void CancelAllTasksOfType(WorkType type)
    {
        var tasksToCancel = pendingTasks.Where(t => t.type == type).ToList();
        
        foreach (var task in tasksToCancel)
        {
            UnregisterWork(task.target);
        }
    }
    
    private void ShowWorkMarker(WorkTask task)
    {
        // 작업 위치에 시각적 마커 표시 (구현 필요)
        // 예: 작업 타입에 따라 다른 아이콘이나 색상의 마커
    }
    
    private void RemoveWorkMarker(WorkTask task)
    {
        // 작업 마커 제거 (구현 필요)
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugInfo) return;
        
        // 대기 중인 작업 표시
        Gizmos.color = Color.yellow;
        foreach (var task in pendingTasks)
        {
            Gizmos.DrawWireCube(task.position, Vector3.one * 0.3f);
        }
        
        // 진행 중인 작업 표시
        Gizmos.color = Color.green;
        foreach (var task in activeTasks)
        {
            Gizmos.DrawWireSphere(task.position, 0.4f);
            
            // 직원과 작업 연결선
            if (task.assignedWorker != null)
            {
                Gizmos.DrawLine(task.assignedWorker.transform.position, task.position);
            }
        }
    }
    
    #endregion
    
    // Public 프로퍼티
    public List<Employee> AllEmployees => allEmployees;
    public List<Employee> IdleEmployees => idleEmployees;
    public int PendingTaskCount => pendingTasks.Count;
    public int ActiveTaskCount => activeTasks.Count;
    
    public WorkStatistics GetStatistics()
    {
        return new WorkStatistics
        {
            totalEmployees = allEmployees.Count,
            idleEmployees = idleEmployees.Count,
            workingEmployees = allEmployees.Count - idleEmployees.Count,
            pendingTasks = pendingTasks.Count,
            activeTasks = activeTasks.Count,
            tasksByType = pendingTasks.GroupBy(t => t.type)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

// 작업 우선순위 설정
[System.Serializable]
public class WorkPrioritySettings
{
    [System.Serializable]
    public class PriorityEntry
    {
        public WorkType workType;
        [Range(1, 10)]
        public int priority = 5; // 낮을수록 우선순위 높음
    }
    
    public List<PriorityEntry> priorities = new List<PriorityEntry>
    {
        new PriorityEntry { workType = WorkType.Mining, priority = 3 },
        new PriorityEntry { workType = WorkType.Chopping, priority = 4 },
        new PriorityEntry { workType = WorkType.Crafting, priority = 2 },
        new PriorityEntry { workType = WorkType.Research, priority = 2 },
        new PriorityEntry { workType = WorkType.Gardening, priority = 5 },
        new PriorityEntry { workType = WorkType.Building, priority = 1 },
        new PriorityEntry { workType = WorkType.Demolish, priority = 6 },
        new PriorityEntry { workType = WorkType.Hauling, priority = 7 }
    };
    
    public int GetPriority(WorkType type)
    {
        var entry = priorities.FirstOrDefault(p => p.workType == type);
        return entry?.priority ?? 5;
    }
}

// 작업 통계
public class WorkStatistics
{
    public int totalEmployees;
    public int idleEmployees;
    public int workingEmployees;
    public int pendingTasks;
    public int activeTasks;
    public Dictionary<WorkType, int> tasksByType;
}

// 작업 태스크
[System.Serializable]
public class WorkTask
{
    public IWorkTarget target;
    public WorkType type;
    public Vector3 position;
    public int priority;
    public float createdTime;
    public float startTime;
    public Employee assignedWorker;
}