using UnityEngine;

public class WorkAssignmentManager : DestroySingleton<WorkAssignmentManager>
{
    [Header("프리팹 설정")]
    [SerializeField] private GameObject panelPrefab; // WorkAssignmentPanel 스크립트가 붙은 프리팹
    [SerializeField] private Transform canvasTransform; // UI가 생성될 캔버스 부모

    private GameObject currentPanelObject;
    private WorkAssignmentPanel currentPanelScript;
    private WorkOrder currentWorkOrder;
    private WorkOrderVisual currentVisual; // 선택된 외곽선 객체 (선택 해제용)

    /// <summary>
    /// 외부(Visual)에서 호출하여 UI를 엽니다.
    /// </summary>
    public void ShowAssignmentUI(WorkOrder order, WorkOrderVisual visual, Vector3 screenPos)
    {
        // 이미 열려있다면 닫고 새로 엽니다 (혹은 갱신)
        CloseUI();

        currentWorkOrder = order;
        currentVisual = visual;

        // UI 생성
        CreatePanel(screenPos);
    }

    private void CreatePanel(Vector3 screenPos)
    {
        if (panelPrefab == null || canvasTransform == null)
        {
            Debug.LogError("WorkAssignmentManager: 프리팹 또는 캔버스가 연결되지 않았습니다.");
            return;
        }

        currentPanelObject = Instantiate(panelPrefab, canvasTransform);
        currentPanelScript = currentPanelObject.GetComponent<WorkAssignmentPanel>();

        // 위치 설정 (마우스 옆)
        RectTransform rect = currentPanelObject.GetComponent<RectTransform>();
        rect.position = screenPos + new Vector3(rect.rect.width / 2 + 20f, -rect.rect.height / 2, 0);
        
        // 화면 밖으로 나가지 않게 보정 (선택사항)
        ClampToScreen(rect);

        // ★ 핵심: UI에게 데이터와 "클릭되면 실행할 함수(로직)"를 넘겨줍니다.
        currentPanelScript.Setup(currentWorkOrder, OnWorkerToggled, CloseUI, OnCancelOrder);
    }
    
    private void OnCancelOrder()
    {
        if (currentWorkOrder != null)
        {
            Debug.Log($"[Manager] 작업 취소 요청: {currentWorkOrder.orderName}");
            
            // 1. 작업물 매니저를 통해 삭제 (내부적으로 Cancel() 호출 및 비주얼 삭제됨)
            WorkOrderManager.instance.RemoveWorkOrder(currentWorkOrder);
            
            // 2. UI 닫기
            CloseUI();
        }
    }
    
    // 직원이 클릭되었을 때 실행될 실제 로직
    private void OnWorkerToggled(Employee employee)
    {
        if (currentWorkOrder == null) return;

        if (currentWorkOrder.IsWorkerAssigned(employee))
        {
            // 할당 해제 로직
            currentWorkOrder.UnassignWorker(employee);
            employee.CancelWork();
        }
        else
        {
            // 할당 시도 로직
            if (currentWorkOrder.assignedWorkers.Count < currentWorkOrder.maxAssignedWorkers)
            {
                bool success = WorkManager.instance.AssignEmployeeToOrder(employee, currentWorkOrder);
                if (!success) Debug.LogWarning("할당 실패");
            }
            else
            {
                Debug.Log("인원 가득 참");
            }
        }

        // 로직 처리 후 UI만 갱신 요청
        currentPanelScript.RefreshUI();
    }

    public void CloseUI()
    {
        if (currentPanelObject != null)
        {
            Destroy(currentPanelObject);
            currentPanelObject = null;
        }

        // 선택된 비주얼이 있었다면 선택 해제 알림
        if (currentVisual != null)
        {
            currentVisual.Deselect();
            currentVisual = null;
        }
        
        currentWorkOrder = null;
    }
    
    private void ClampToScreen(RectTransform rect)
    {
        Vector3 pos = rect.position;
        float width = rect.rect.width;
        float height = rect.rect.height;

        if (pos.x + width/2 > Screen.width) pos.x = Screen.width - width/2;
        if (pos.y - height/2 < 0) pos.y = height/2;
        
        rect.position = pos;
    }
}