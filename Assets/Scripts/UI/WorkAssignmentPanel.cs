using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class WorkAssignmentPanel : MonoBehaviour
{
    [Header("UI 요소 연결")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject employeeRowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;

    [Header("색상 설정")]
    [SerializeField] private Color assignedColor = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private Color unassignedColor = new Color(1f, 1f, 1f);

    // 매니저에게 클릭 이벤트를 전달할 액션 (콜백)
    private Action<Employee> onEmployeeClickCallback;
    private Action onCloseCallback;
    private Action onCancelWorkCallback;
    
    private WorkOrder currentOrder;

    public void Setup(WorkOrder order, Action<Employee> onEmployeeClick, Action onClose, Action onCancelWork)
    {
        currentOrder = order;
        onEmployeeClickCallback = onEmployeeClick;
        onCloseCallback = onClose;

        if(closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => onCloseCallback?.Invoke());
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => {
                // "정말 취소하시겠습니까?" 팝업을 띄울 수도 있음
                onCancelWorkCallback?.Invoke();
            });
        }

        RefreshUI();
    }

    // 외부(매니저)에서 데이터가 변경되었을 때 화면만 갱신
    public void RefreshUI()
    {
        if (currentOrder == null) return;

        UpdateHeader();
        UpdateList();
    }

    private void UpdateHeader()
    {
        if (headerText != null)
        {
            headerText.text = $"{currentOrder.orderName}\n({currentOrder.assignedWorkers.Count}/{currentOrder.maxAssignedWorkers})";
        }
    }

    private void UpdateList()
    {
        // 기존 목록 클리어
        foreach (Transform child in listContent) Destroy(child.gameObject);

        // 직원 목록 생성
        List<Employee> employees = EmployeeManager.instance.AllEmployees;

        foreach (var emp in employees)
        {
            GameObject row = Instantiate(employeeRowPrefab, listContent);
            
            // 이름 설정
            TextMeshProUGUI nameText = row.GetComponentInChildren<TextMeshProUGUI>();
            if(nameText != null) nameText.text = emp.Data.employeeName;

            // 배경색 설정 (할당 여부에 따라)
            Image bg = row.GetComponent<Image>();
            bool isAssigned = currentOrder.IsWorkerAssigned(emp);
            if (bg != null) bg.color = isAssigned ? assignedColor : unassignedColor;

            // 버튼 클릭 시 매니저가 넘겨준 콜백 실행
            Button btn = row.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => onEmployeeClickCallback?.Invoke(emp));
            }
        }
    }
    
    void Update()
    {
        // ESC나 우클릭으로 닫기 요청
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            onCloseCallback?.Invoke();
        }
    }
}