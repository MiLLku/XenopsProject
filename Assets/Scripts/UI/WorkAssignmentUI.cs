using System.Linq;
using UnityEngine;

/// <summary>
/// 작업 할당 UI (임시 구현 - 나중에 확장)
/// </summary>
public class WorkAssignmentUI : DestroySingleton<WorkAssignmentUI>
{
    public void OpenAssignmentUI(WorkOrder order)
    {
        Debug.Log($"[WorkAssignmentUI] 작업 할당 UI 열기: {order.orderName}");
        Debug.Log("TODO: 직원 선택 UI 구현 필요");
        
        // 임시: 첫 번째 유휴 직원에게 자동 할당
        if (WorkSystemManager.instance != null && EmployeeManager.instance != null)
        {
            var idleEmployees = EmployeeManager.instance.AllEmployees
                .Where(e => e.State == EmployeeState.Idle && e.CanPerformWork(order.workType))
                .ToList();
            
            if (idleEmployees.Count > 0)
            {
                WorkSystemManager.instance.AssignEmployeeToOrder(idleEmployees[0], order);
                Debug.Log($"임시 할당: {idleEmployees[0].Data.employeeName} -> {order.orderName}");
            }
            else
            {
                Debug.LogWarning("할당 가능한 유휴 직원이 없습니다.");
            }
        }
    }
}