using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 작업 시스템 통합 매니저
/// - 작업물(WorkOrder) 생성/삭제/관리
/// - 직원 등록/할당/작업 완료 처리
/// - 작업 할당 UI 관리
/// </summary>
public class WorkSystemManager : DestroySingleton<WorkSystemManager>
{
    #region 직렬화 필드
    
    [Header("작업물 관리")]
    [SerializeField] private List<WorkOrder> allOrders = new List<WorkOrder>();
    [SerializeField] private int nextOrderId = 1;
    
    [Header("비주얼 설정")]
    [SerializeField] private GameObject workOrderVisualPrefab;
    [SerializeField] private Transform visualParent;
    
    [Header("UI 설정")]
    [SerializeField] private GameObject assignmentPanelPrefab;
    [SerializeField] private Transform canvasTransform;
    
    [Header("직원 관리")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    #endregion
    
    #region 내부 변수
    
    // 작업물 ID -> 비주얼 매핑
    private Dictionary<int, WorkOrderVisual> orderVisuals = new Dictionary<int, WorkOrderVisual>();
    
    // 직원별 현재 작업물 매핑
    private Dictionary<Employee, WorkOrder> employeeToOrderMap = new Dictionary<Employee, WorkOrder>();
    
    // 직원별 현재 작업 Task 매핑
    private Dictionary<Employee, WorkTask> employeeToTaskMap = new Dictionary<Employee, WorkTask>();
    
    // UI 관련
    private GameObject currentPanelObject;
    private WorkAssignmentPanel currentPanelScript;
    private WorkOrder currentUIOrder;
    private WorkOrderVisual currentUIVisual;
    
    #endregion
    
    #region Unity 생명주기
    
    protected override void Awake()
    {
        base.Awake();
        
        // 비주얼 부모 오브젝트 생성
        if (visualParent == null)
        {
            GameObject parent = new GameObject("WorkOrderVisuals");
            visualParent = parent.transform;
        }
    }
    
    void Start()
    {
        // EmployeeManager 연동
        if (EmployeeManager.instance != null)
        {
            foreach (var employee in EmployeeManager.instance.AllEmployees)
            {
                RegisterEmployee(employee);
            }
            
            EmployeeManager.instance.OnEmployeeSpawned += RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved += UnregisterEmployee;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] 직원 {EmployeeManager.instance.EmployeeCount}명 등록 완료");
            }
        }
    }
    
    void Update()
    {
        // 주기적으로 완료된 작업물 정리 (5초마다)
        if (Time.frameCount % 300 == 0)
        {
            CleanupCompletedOrders();
            CleanupInvalidTasks();
        }
    }
    
    void OnDestroy()
    {
        if (EmployeeManager.instance != null)
        {
            EmployeeManager.instance.OnEmployeeSpawned -= RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved -= UnregisterEmployee;
        }
    }
    
    #endregion
    
    #region 작업물(WorkOrder) 관리
    
    /// <summary>
    /// 새 작업물을 생성합니다.
    /// </summary>
    public WorkOrder CreateWorkOrder(string name, WorkType workType, int maxWorkers, int priority = 5)
    {
        WorkOrder order = new WorkOrder
        {
            orderId = nextOrderId++,
            orderName = name,
            workType = workType,
            maxAssignedWorkers = maxWorkers,
            priority = priority,
            createdTime = Time.time,
            isActive = true,
            isPaused = false
        };
        
        allOrders.Add(order);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] 작업물 생성: {name} (ID: {order.orderId})");
        }
        
        return order;
    }
    
    /// <summary>
    /// 작업물에 비주얼을 생성합니다 (타일 위치 포함).
    /// </summary>
    public WorkOrderVisual CreateWorkOrderWithVisual(string name, WorkType workType, int maxWorkers, 
        List<Vector3Int> tiles, int priority = 5)
    {
        WorkOrder order = CreateWorkOrder(name, workType, maxWorkers, priority);
        
        if (workOrderVisualPrefab == null)
        {
            Debug.LogWarning("[WorkSystemManager] WorkOrderVisual 프리팹이 설정되지 않았습니다!");
            return null;
        }
        
        // 비주얼 오브젝트 생성
        GameObject visualObj = Instantiate(workOrderVisualPrefab, visualParent);
        visualObj.name = $"WorkOrder_{order.orderId}_{name}";
        
        WorkOrderVisual visual = visualObj.GetComponent<WorkOrderVisual>();
        if (visual != null)
        {
            visual.Initialize(order, tiles);
            orderVisuals[order.orderId] = visual;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] 작업물 비주얼 생성: {name} (타일 {tiles.Count}개)");
            }
        }
        
        return visual;
    }
    
    /// <summary>
    /// 특정 좌표가 현재 진행 중인 작업에 포함되어 있는지 확인합니다.
    /// </summary>
    public bool IsTileUnderWork(Vector3Int tilePos)
    {
        foreach (var order in allOrders)
        {
            if (!order.isActive || order.IsCompleted()) continue;
            
            if (order.taskQueue.HasTaskAtPosition(tilePos))
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 작업물을 삭제합니다.
    /// </summary>
    public void RemoveWorkOrder(WorkOrder order)
    {
        if (order == null) return;
        
        order.Cancel();
        allOrders.Remove(order);
        
        // 비주얼도 삭제
        if (orderVisuals.ContainsKey(order.orderId))
        {
            WorkOrderVisual visual = orderVisuals[order.orderId];
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }
            orderVisuals.Remove(order.orderId);
        }
        
        // 해당 작업물에 할당된 직원들 매핑 제거
        var employeesToRemove = employeeToOrderMap
            .Where(kvp => kvp.Value == order)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var emp in employeesToRemove)
        {
            employeeToOrderMap.Remove(emp);
            employeeToTaskMap.Remove(emp);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] 작업물 삭제: {order.orderName}");
        }
    }
    
    /// <summary>
    /// 완료된 작업물을 자동으로 제거합니다.
    /// </summary>
    public void CleanupCompletedOrders()
    {
        var completedOrders = allOrders.Where(o => o.IsCompleted()).ToList();
        
        foreach (var order in completedOrders)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] 작업물 완료: {order.orderName}");
            }
            RemoveWorkOrder(order);
        }
    }
    
    /// <summary>
    /// 유효하지 않은 작업 정리
    /// </summary>
    public void CleanupInvalidTasks()
    {
        foreach (var order in allOrders)
        {
            order.CleanupInvalidTasks();
        }
    }
    
    /// <summary>
    /// 우선순위별로 정렬된 활성 작업물 목록을 반환합니다.
    /// </summary>
    public List<WorkOrder> GetActiveOrders()
    {
        return allOrders
            .Where(o => o.isActive && !o.isPaused)
            .OrderBy(o => o.priority)
            .ThenBy(o => o.createdTime)
            .ToList();
    }
    
    /// <summary>
    /// 특정 작업 타입의 작업물을 반환합니다.
    /// </summary>
    public List<WorkOrder> GetOrdersByType(WorkType type)
    {
        return allOrders.Where(o => o.workType == type && o.isActive && !o.isPaused).ToList();
    }
    
    /// <summary>
    /// ID로 작업물을 찾습니다.
    /// </summary>
    public WorkOrder GetOrderById(int id)
    {
        return allOrders.FirstOrDefault(o => o.orderId == id);
    }
    
    /// <summary>
    /// ID로 작업물 비주얼을 찾습니다.
    /// </summary>
    public WorkOrderVisual GetVisualById(int id)
    {
        orderVisuals.TryGetValue(id, out WorkOrderVisual visual);
        return visual;
    }
    
    #endregion
    
    #region 직원 관리
    
    public void RegisterEmployee(Employee employee)
    {
        if (employee == null || allEmployees.Contains(employee)) return;
        
        allEmployees.Add(employee);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] 직원 등록: {employee.Data.employeeName}");
        }
    }
    
    public void UnregisterEmployee(Employee employee)
    {
        if (employee == null || !allEmployees.Contains(employee)) return;
        
        allEmployees.Remove(employee);
        
        // 해당 직원이 작업 중이던 작업물에서 제거
        if (employeeToOrderMap.ContainsKey(employee))
        {
            WorkOrder order = employeeToOrderMap[employee];
            order.UnassignWorker(employee);
            employeeToOrderMap.Remove(employee);
        }
        
        employeeToTaskMap.Remove(employee);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] 직원 제거: {employee.Data.employeeName}");
        }
    }
    
    #endregion
    
    #region 작업 할당
    
    /// <summary>
    /// 플레이어가 특정 직원을 특정 작업물에 수동으로 할당합니다.
    /// </summary>
    public bool AssignEmployeeToOrder(Employee employee, WorkOrder order)
    {
        if (employee == null || order == null)
        {
            Debug.LogWarning("[WorkSystemManager] 직원 또는 작업물이 null입니다.");
            return false;
        }
        
        // 직원이 해당 작업을 수행할 수 있는지 확인
        if (!employee.CanPerformWork(order.workType))
        {
            Debug.LogWarning($"[WorkSystemManager] {employee.Data.employeeName}은(는) {order.workType} 작업을 수행할 수 없습니다.");
            return false;
        }
        
        // 작업물에 더 이상 작업자를 할당할 수 없는 경우
        if (!order.CanAssignWorker())
        {
            Debug.LogWarning($"[WorkSystemManager] 작업물 '{order.orderName}'에 더 이상 작업자를 할당할 수 없습니다.");
            return false;
        }
        
        // 직원이 이미 다른 작업 중이면 취소
        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }
        
        // 작업물에 직원 등록
        if (!order.AssignWorker(employee))
        {
            return false;
        }
        
        // 매핑 저장
        employeeToOrderMap[employee] = order;
        
        // 큐에서 다음 작업 할당
        if (AssignNextTaskFromQueue(employee, order))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] {employee.Data.employeeName}을(를) '{order.orderName}'에 할당 완료");
            }
            return true;
        }
        else
        {
            // 할당 실패 시 롤백
            order.UnassignWorker(employee);
            employeeToOrderMap.Remove(employee);
            return false;
        }
    }
    
    /// <summary>
    /// 작업물의 큐에서 직원에게 다음 작업을 할당합니다.
    /// </summary>
    private bool AssignNextTaskFromQueue(Employee worker, WorkOrder order)
    {
        // 큐에서 가장 적합한 작업 가져오기
        WorkTask task = order.AssignNextTaskToWorker(worker);
        
        if (task == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[WorkSystemManager] 작업물 '{order.orderName}'에 할당 가능한 작업이 없습니다.");
            }
            return false;
        }
        
        // 작업 매핑 저장
        employeeToTaskMap[worker] = task;
        
        // 작업 시작
        task.Start();
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}에게 작업 할당: Task {task.taskId} at {task.GetPosition()}");
        }
        
        // 직원에게 실제 작업 할당
        worker.AssignWork(order, task.target);
        
        return true;
    }
    
    /// <summary>
    /// 직원의 할당을 해제합니다.
    /// </summary>
    public void UnassignEmployee(Employee employee)
    {
        if (employee == null) return;
        
        // 현재 작업 Task가 있으면 할당 해제
        if (employeeToTaskMap.TryGetValue(employee, out WorkTask task))
        {
            task.Unassign();
            employeeToTaskMap.Remove(employee);
        }
        
        if (employeeToOrderMap.TryGetValue(employee, out WorkOrder order))
        {
            order.UnassignWorker(employee);
            employeeToOrderMap.Remove(employee);
        }
        
        employee.CancelWork();
    }
    
    #endregion
    
    #region 작업 완료 처리
    
    /// <summary>
    /// 직원이 작업을 완료했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCompletedTarget(Employee worker, IWorkTarget target, WorkOrder order)
    {
        if (worker == null || order == null) return;
        
        // 현재 작업 Task 찾기
        WorkTask completedTask = null;
        if (employeeToTaskMap.TryGetValue(worker, out completedTask))
        {
            // 작업물의 큐에서 완료 처리
            order.CompleteTask(completedTask);
            employeeToTaskMap.Remove(worker);
        }
        else
        {
            // 기존 방식으로 폴백
            order.CompleteTarget(target, worker);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}이(가) 작업 완료. " +
                     $"진행률: {order.GetProgress() * 100:F0}%");
        }
        
        // 작업물이 완전히 완료되었는지 확인
        if (order.IsCompleted())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] 작업물 '{order.orderName}' 완전 완료!");
            }
            
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
            employeeToTaskMap.Remove(worker);
            
            RemoveWorkOrder(order);
            return;
        }
        
        // 같은 작업물에서 다음 작업 할당 (큐에서)
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}에게 다음 작업 할당 시도...");
        }

        if (!AssignNextTaskFromQueue(worker, order))
        {
            // 더 이상 할당할 작업이 없으면 작업자 해제
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}: 더 이상 할당할 작업 없음. 작업자 해제.");
            }
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
            employeeToTaskMap.Remove(worker);
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}에게 다음 작업 할당 성공!");
            }
        }
    }
    
    /// <summary>
    /// 직원이 작업을 취소했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCancelledWork(Employee worker)
    {
        if (worker == null) return;
        
        // 현재 작업 Task가 있으면 할당 해제
        if (employeeToTaskMap.TryGetValue(worker, out WorkTask task))
        {
            task.Unassign();
            employeeToTaskMap.Remove(worker);
        }
        
        if (employeeToOrderMap.TryGetValue(worker, out WorkOrder order))
        {
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkSystemManager] {worker.Data.employeeName}의 작업 취소");
        }
    }
    
    #endregion
    
    #region UI 관리
    
    /// <summary>
    /// 작업 할당 UI를 표시합니다. (WorkOrderVisual에서 호출)
    /// </summary>
    public void ShowAssignmentUI(WorkOrder order, WorkOrderVisual visual, Vector3 screenPos)
    {
        CloseAssignmentUI();
        
        currentUIOrder = order;
        currentUIVisual = visual;
        
        CreateAssignmentPanel(screenPos);
    }
    
    private void CreateAssignmentPanel(Vector3 screenPos)
    {
        if (assignmentPanelPrefab == null || canvasTransform == null)
        {
            Debug.LogError("[WorkSystemManager] 패널 프리팹 또는 캔버스가 연결되지 않았습니다.");
            return;
        }
        
        currentPanelObject = Instantiate(assignmentPanelPrefab, canvasTransform);
        currentPanelScript = currentPanelObject.GetComponent<WorkAssignmentPanel>();
        
        // 위치 설정
        RectTransform rect = currentPanelObject.GetComponent<RectTransform>();
        rect.position = screenPos + new Vector3(rect.rect.width / 2 + 20f, -rect.rect.height / 2, 0);
        ClampToScreen(rect);
        
        // UI 초기화
        currentPanelScript.Setup(currentUIOrder, OnWorkerToggled, CloseAssignmentUI, OnCancelOrder);
    }
    
    private void OnWorkerToggled(Employee employee)
    {
        if (currentUIOrder == null) return;
        
        if (currentUIOrder.IsWorkerAssigned(employee))
        {
            // 할당 해제
            UnassignEmployee(employee);
        }
        else
        {
            // 할당 시도
            if (currentUIOrder.assignedWorkers.Count < currentUIOrder.maxAssignedWorkers)
            {
                bool success = AssignEmployeeToOrder(employee, currentUIOrder);
                if (!success)
                {
                    Debug.LogWarning("[WorkSystemManager] 할당 실패");
                }
            }
            else
            {
                Debug.Log("[WorkSystemManager] 인원 가득 참");
            }
        }
        
        // UI 갱신
        currentPanelScript?.RefreshUI();
    }
    
    private void OnCancelOrder()
    {
        if (currentUIOrder != null)
        {
            Debug.Log($"[WorkSystemManager] 작업 취소 요청: {currentUIOrder.orderName}");
            RemoveWorkOrder(currentUIOrder);
            CloseAssignmentUI();
        }
    }
    
    public void CloseAssignmentUI()
    {
        if (currentPanelObject != null)
        {
            Destroy(currentPanelObject);
            currentPanelObject = null;
        }
        
        if (currentUIVisual != null)
        {
            currentUIVisual.Deselect();
            currentUIVisual = null;
        }
        
        currentUIOrder = null;
    }
    
    private void ClampToScreen(RectTransform rect)
    {
        Vector3 pos = rect.position;
        float width = rect.rect.width;
        float height = rect.rect.height;
        
        if (pos.x + width / 2 > Screen.width) pos.x = Screen.width - width / 2;
        if (pos.y - height / 2 < 0) pos.y = height / 2;
        
        rect.position = pos;
    }
    
    #endregion
    
    #region 유틸리티
    
    /// <summary>
    /// 특정 직원의 현재 작업물 반환
    /// </summary>
    public WorkOrder GetEmployeeOrder(Employee employee)
    {
        employeeToOrderMap.TryGetValue(employee, out WorkOrder order);
        return order;
    }
    
    /// <summary>
    /// 특정 직원의 현재 작업 Task 반환
    /// </summary>
    public WorkTask GetEmployeeTask(Employee employee)
    {
        employeeToTaskMap.TryGetValue(employee, out WorkTask task);
        return task;
    }
    
    /// <summary>
    /// 작업 통계 반환
    /// </summary>
    public WorkStatistics GetStatistics()
    {
        int pendingTasks = 0;
        int completedTasks = 0;
        
        foreach (var order in allOrders)
        {
            pendingTasks += order.taskQueue.PendingCount;
            completedTasks += order.taskQueue.CompletedCount;
        }
        
        return new WorkStatistics
        {
            totalEmployees = allEmployees.Count,
            idleEmployees = allEmployees.Count(e => e.State == EmployeeState.Idle),
            workingEmployees = allEmployees.Count(e => e.State == EmployeeState.Working),
            activeOrders = ActiveOrderCount,
            pendingTasks = pendingTasks,
            completedTasks = completedTasks
        };
    }
    
    #endregion
    
    #region Public 프로퍼티
    
    public List<WorkOrder> AllOrders => allOrders;
    public int ActiveOrderCount => allOrders.Count(o => o.isActive && !o.isPaused);
    public List<Employee> AllEmployees => allEmployees;
    public List<Employee> IdleEmployees => allEmployees.Where(e => e.State == EmployeeState.Idle).ToList();
    
    #endregion
}