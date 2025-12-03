using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원의 작업 상태를 관리하는 매니저 (플레이어 수동 할당 전용)
/// </summary>
public class WorkManager : DestroySingleton<WorkManager>
{
    [Header("직원 관리")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    private WorkOrderManager orderManager;
    
    // 직원별 현재 작업 중인 작업물 추적
    private Dictionary<Employee, WorkOrder> employeeToOrderMap = new Dictionary<Employee, WorkOrder>();
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    void Start()
    {
        orderManager = WorkOrderManager.instance;
        if (orderManager == null)
        {
            Debug.LogError("[WorkManager] WorkOrderManager를 찾을 수 없습니다!");
        }
        
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
                Debug.Log($"[WorkManager] 직원 {EmployeeManager.instance.EmployeeCount}명 등록 완료");
            }
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
    
    #region 직원 관리
    
    public void RegisterEmployee(Employee employee)
    {
        if (employee == null || allEmployees.Contains(employee)) return;
        
        allEmployees.Add(employee);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 등록: {employee.Data.employeeName}");
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
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 제거: {employee.Data.employeeName}");
        }
    }
    
    #endregion
    
    #region 수동 작업 할당
    
    /// <summary>
    /// 플레이어가 특정 직원을 특정 작업물에 수동으로 할당합니다.
    /// </summary>
    public bool AssignEmployeeToOrder(Employee employee, WorkOrder order)
    {
        if (employee == null || order == null)
        {
            Debug.LogWarning("[WorkManager] 직원 또는 작업물이 null입니다.");
            return false;
        }
        
        // 직원이 해당 작업을 수행할 수 있는지 확인
        if (!employee.CanPerformWork(order.workType))
        {
            Debug.LogWarning($"[WorkManager] {employee.Data.employeeName}은(는) {order.workType} 작업을 수행할 수 없습니다.");
            return false;
        }
        
        // 작업물에 더 이상 작업자를 할당할 수 없는 경우
        if (!order.CanAssignWorker())
        {
            Debug.LogWarning($"[WorkManager] 작업물 '{order.orderName}'에 더 이상 작업자를 할당할 수 없습니다.");
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
        
        // 구체적인 작업 대상 할당
        if (AssignSpecificTarget(employee, order))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {employee.Data.employeeName}을(를) '{order.orderName}'에 할당 완료");
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
    /// 직원에게 구체적인 작업 대상을 할당합니다.
    /// 1순위: 현재 위치에서 작업 가능한 범위 내 타겟
    /// 2순위: 가장 가까운 타겟
    /// </summary>
    private bool AssignSpecificTarget(Employee worker, WorkOrder order)
    {
        var availableTargets = order.GetAvailableTargets();

        if (availableTargets.Count == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[WorkManager] 작업물 '{order.orderName}'에 가용 작업 대상이 없습니다.");
            }
            return false;
        }

        IWorkTarget selectedTarget = null;
        Vector3 workerPosition = worker.transform.position;

        // 1순위: 현재 위치에서 작업 가능한 범위 내 타겟 찾기
        foreach (var target in availableTargets)
        {
            Vector3 targetPos = target.GetWorkPosition();
            Vector3Int targetTilePos = new Vector3Int(
                Mathf.FloorToInt(targetPos.x),
                Mathf.FloorToInt(targetPos.y),
                0
            );

            if (worker.IsPositionInWorkRange(targetTilePos))
            {
                selectedTarget = target;
                if (showDebugInfo)
                {
                    Debug.Log($"[WorkManager] {worker.Data.employeeName}의 작업 범위 내 타겟 발견: {targetTilePos}");
                }
                break;
            }
        }

        // 2순위: 작업 범위 내에 없으면 가장 가까운 타겟 선택
        if (selectedTarget == null)
        {
            selectedTarget = availableTargets
                .OrderBy(t =>
                {
                    Vector3 targetPos = t.GetWorkPosition();
                    Vector3Int targetTilePos = new Vector3Int(
                        Mathf.FloorToInt(targetPos.x),
                        Mathf.FloorToInt(targetPos.y),
                        0
                    );

                    // 작업 가능한 위치까지의 거리 계산
                    Vector3 workablePos = FindWorkablePositionNearTarget(workerPosition, targetTilePos);
                    return Vector3.Distance(workerPosition, workablePos);
                })
                .First();

            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {worker.Data.employeeName}에게 가장 가까운 타겟 할당: {selectedTarget.GetWorkPosition()}");
            }
        }

        // 작업물에 할당 기록
        order.AssignTargetToWorker(worker, selectedTarget);

        // 직원에게 작업 할당
        worker.AssignWork(order, selectedTarget);

        return true;
    }

    /// <summary>
    /// 작업 대상 근처에서 직원이 실제로 서 있을 수 있는 위치를 찾습니다.
    /// </summary>
    private Vector3 FindWorkablePositionNearTarget(Vector3 workerPos, Vector3Int targetTilePos)
    {
        // 작업 대상 타일 주변 8방향 + 상하 위치 확인
        Vector3Int[] offsets = new Vector3Int[]
        {
            new Vector3Int(-1, 0, 0),  // 왼쪽
            new Vector3Int(1, 0, 0),   // 오른쪽
            new Vector3Int(0, -1, 0),  // 아래
            new Vector3Int(-1, -1, 0), // 왼쪽 아래
            new Vector3Int(1, -1, 0),  // 오른쪽 아래
            new Vector3Int(-1, 1, 0),  // 왼쪽 위
            new Vector3Int(1, 1, 0),   // 오른쪽 위
            new Vector3Int(0, 1, 0),   // 위
        };

        Vector3Int workerTilePos = new Vector3Int(
            Mathf.FloorToInt(workerPos.x),
            Mathf.FloorToInt(workerPos.y),
            0
        );

        // 현재 위치에서 가장 가까운 작업 가능 위치 찾기
        Vector3Int bestPos = targetTilePos;
        float minDist = float.MaxValue;

        foreach (var offset in offsets)
        {
            Vector3Int checkPos = targetTilePos + offset;

            // 해당 위치에서 타겟을 작업할 수 있는지 확인
            int dx = Mathf.Abs(targetTilePos.x - checkPos.x);
            int dy = targetTilePos.y - checkPos.y;

            // 작업 범위 내인지 확인 (좌우 1칸, 상 3칸, 하 1칸)
            if (dx <= 1 && dy >= -1 && dy <= 3)
            {
                float dist = Vector3Int.Distance(workerTilePos, checkPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestPos = checkPos;
                }
            }
        }

        // 타일 중심이 아닌 타일 위 (y + 1)로 반환
        return new Vector3(bestPos.x + 0.5f, bestPos.y + 1f, 0);
    }
    
    #endregion
    
    #region 작업 완료 처리
    
    /// <summary>
    /// 직원이 작업을 완료했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCompletedTarget(Employee worker, IWorkTarget target, WorkOrder order)
    {
        if (worker == null || order == null) return;
        
        // 작업물에서 해당 대상 완료 처리
        order.CompleteTarget(target, worker);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}이(가) 작업 완료. " +
                     $"진행률: {order.GetProgress() * 100:F0}%");
        }
        
        // 작업물이 완전히 완료되었는지 확인
        if (order.IsCompleted())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] 작업물 '{order.orderName}' 완전 완료!");
            }
            
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
            
            if (orderManager != null)
            {
                orderManager.RemoveWorkOrder(order);
            }
            
            return;
        }
        
        // 같은 작업물에서 다음 작업 대상 할당
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}에게 다음 작업 대상 할당 시도...");
        }

        if (!AssignSpecificTarget(worker, order))
        {
            // 더 이상 할당할 작업이 없으면 작업자 해제
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {worker.Data.employeeName}: 더 이상 할당할 작업 없음. 작업자 해제.");
            }
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {worker.Data.employeeName}에게 다음 작업 할당 성공!");
            }
        }
    }
    
    /// <summary>
    /// 직원이 작업을 취소했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCancelledWork(Employee worker)
    {
        if (worker == null || !employeeToOrderMap.ContainsKey(worker))
            return;
        
        WorkOrder order = employeeToOrderMap[worker];
        order.UnassignWorker(worker);
        employeeToOrderMap.Remove(worker);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}의 작업 취소");
        }
    }
    
    #endregion
    
    // Public 프로퍼티
    public List<Employee> AllEmployees => allEmployees;
    public List<Employee> IdleEmployees => allEmployees.Where(e => e.State == EmployeeState.Idle).ToList();
}