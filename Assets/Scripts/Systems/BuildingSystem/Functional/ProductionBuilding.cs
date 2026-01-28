using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생산 건물 추상 클래스 
/// 제재소, 대장간, 연금술 작업대 등 모든 생산 건물의 기본 클래스
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class ProductionBuilding : MonoBehaviour, IBuildingFunction
{
    [Header("제작 설정")]
    [Tooltip("이 건물에서 만들 수 있는 레시피 목록")]
    [SerializeField] protected List<CraftingRecipe> availableRecipes;

    [Header("작업 위치")]
    [Tooltip("직원이 작업할 위치 (빈 값이면 건물 중앙)")]
    [SerializeField] protected Transform workPosition;

    [Header("시각 효과 (선택사항)")]
    [SerializeField] protected GameObject workingEffectPrefab;
    [SerializeField] protected Vector3 effectOffset = new Vector3(0, 0.5f, 0);

    [Header("오디오 (선택사항)")]
    [SerializeField] protected AudioClip workingSound;

    // 상태 관리
    protected CraftingOrder _currentOrder;
    protected CraftingRecipe _pendingRecipe; // 직원 할당 대기 중인 레시피
    protected int _pendingAmount; // 직원 할당 대기 중인 수량
    protected GameObject _workingEffectInstance;
    protected Building _building;

    // IBuildingFunction 구현
    public bool IsOperating => _currentOrder != null && !_currentOrder.IsCompleted();

    // Public 프로퍼티
    public List<CraftingRecipe> AvailableRecipes => availableRecipes;
    public CraftingOrder CurrentOrder => _currentOrder;
    public Vector3 WorkPosition => workPosition != null ? workPosition.position : transform.position;
    public bool HasPendingOrder => _pendingRecipe != null;
    public bool HasActiveOrder => _currentOrder != null;

    protected virtual void Awake()
    {
        _building = GetComponent<Building>();

        // 작업 위치가 지정되지 않았으면 건물 중앙 자동 계산
        if (workPosition == null)
        {
            CreateDefaultWorkPosition();
        }
    }

    /// <summary>
    /// 기본 작업 위치 생성 (건물 중앙)
    /// </summary>
    private void CreateDefaultWorkPosition()
    {
        // 빈 GameObject 생성
        GameObject workPosObj = new GameObject("WorkPosition_Auto");
        workPosObj.transform.SetParent(transform);

        // Building 컴포넌트에서 크기 가져오기
        if (_building != null && _building.buildingData != null)
        {
            Vector2Int size = _building.buildingData.size; // buildingSize → size
            // 건물 중앙 바닥으로 설정 (피벗 기준)
            workPosObj.transform.localPosition = new Vector3(size.x * 0.5f, 0, 0);
        }
        else
        {
            // Building 정보 없으면 그냥 피벗 사용
            workPosObj.transform.localPosition = Vector3.zero;
        }

        workPosition = workPosObj.transform;
        Debug.Log($"[{GetBuildingName()}] 자동 작업 위치 생성: {workPosition.position}");
    }

    protected virtual void Start()
    {
        ValidateRecipes();
    }

    protected virtual void OnDestroy()
    {
        // 진행 중인 작업 취소
        if (_currentOrder != null || _pendingRecipe != null)
        {
            CancelProduction();
        }

        // 시각 효과 제거
        if (_workingEffectInstance != null)
        {
            Destroy(_workingEffectInstance);
        }
    }

    private void ValidateRecipes()
    {
        if (availableRecipes == null || availableRecipes.Count == 0)
        {
            Debug.LogWarning($"[{GetBuildingName()}] {name}에 사용 가능한 레시피가 없습니다!");
        }

        // null 레시피 제거
        availableRecipes?.RemoveAll(r => r == null);
    }

    private void OnMouseDown()
    {
        // Building이 비활성화 상태인지 확인
        if (_building != null && !_building.IsFunctional)
        {
            Debug.LogWarning($"[{GetBuildingName()}] 건물이 비활성화 상태입니다. 기반을 복구하세요.");
            ShowDisabledMessage();
            return;
        }

        // Order가 생성되어 직원 할당 대기 중이거나 작업 중이면
        if (_pendingRecipe != null || _currentOrder != null)
        {
            // WorkAssignmentPanel 열기 (직원 재할당/할당)
            OpenWorkAssignmentPanel();
        }
        else
        {
            // ProductionUI 열기 (레시피 선택)
            OpenProductionUI();
        }
    }

    /// <summary>
    /// 생산 UI를 엽니다 (레시피 선택)
    /// </summary>
    protected virtual void OpenProductionUI()
    {
        if (availableRecipes == null || availableRecipes.Count == 0)
        {
            Debug.LogWarning($"[{GetBuildingName()}] 제작 가능한 레시피가 없습니다.");
            return;
        }

        var productionUI = UIManager.instance.GetPanel<ProductionUI>(UIPanelType.ProductionUI);
        if (productionUI != null)
        {
            productionUI.Setup(availableRecipes, OnProductionOrderCreated, this);
            UIManager.instance.ShowPanel(UIPanelType.ProductionUI);
        }
        else
        {
            Debug.LogError($"[{GetBuildingName()}] ProductionUI를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// ProductionUI에서 Order가 생성되었을 때 호출됨
    /// </summary>
    protected virtual void OnProductionOrderCreated(CraftingRecipe recipe, int amount, Employee worker)
    {
        if (recipe == null)
        {
            Debug.LogError($"[{GetBuildingName()}] 레시피가 null입니다.");
            return;
        }

        // 재료 소모
        if (!ConsumeMaterials(recipe, amount))
        {
            Debug.LogError($"[{GetBuildingName()}] 재료 소모 실패");
            return;
        }

        Debug.Log($"[{GetBuildingName()}] 재료 소모 완료. {recipe.outputItem.itemName} x{amount} Order 생성.");

        // Pending 상태로 저장 (직원 할당 대기)
        _pendingRecipe = recipe;
        _pendingAmount = amount;

        // 직원이 이미 선택되어 넘어온 경우 바로 시작
        if (worker != null)
        {
            StartProduction(worker);
        }
    }

    /// <summary>
    /// 재료 소모 (상속 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual bool ConsumeMaterials(CraftingRecipe recipe, int amount)
    {
        if (recipe == null || recipe.requiredMaterials == null) return true;

        foreach (var cost in recipe.requiredMaterials)
        {
            int totalAmount = cost.amount * amount;
            if (!InventoryManager.instance.RemoveItem(cost.item, totalAmount))
            {
                Debug.LogError($"[{GetBuildingName()}] 재료 소모 실패: {cost.item.itemName}");
                // TODO: 이미 소모한 재료 복구 로직
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 직원 할당 패널 열기
    /// </summary>
    protected virtual void OpenWorkAssignmentPanel()
    {
        // Pending Order가 있으면 그것 사용, 아니면 현재 Order 사용
        CraftingRecipe targetRecipe = _pendingRecipe ?? _currentOrder?.recipe;
        int targetAmount = _pendingRecipe != null ? _pendingAmount : (_currentOrder?.craftAmount ?? 1);

        if (targetRecipe == null)
        {
            Debug.LogWarning($"[{GetBuildingName()}] 할당할 Order가 없습니다.");
            return;
        }

        // 임시 WorkOrder 생성 (작업자 선택용)
        WorkOrder tempOrder = new WorkOrder
        {
            orderName = $"{targetRecipe.outputItem.itemName} 제작",
            workType = WorkType.Crafting,
            maxAssignedWorkers = 1
        };

        // WorkAssignmentPanel 가져오기
        var assignmentPanel = UIManager.instance.GetPanel<WorkAssignmentPanel>(UIPanelType.WorkAssignment);
        if (assignmentPanel != null)
        {
            assignmentPanel.Setup(
                tempOrder,
                OnWorkerAssigned,
                OnWorkerAssignmentClosed,
                OnWorkerAssignmentCancelled
            );

            UIManager.instance.ShowPanel(UIPanelType.WorkAssignment);
        }
        else
        {
            Debug.LogError($"[{GetBuildingName()}] WorkAssignmentPanel을 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 직원이 할당되었을 때
    /// </summary>
    protected virtual void OnWorkerAssigned(Employee worker)
    {
        if (worker == null)
        {
            Debug.LogWarning($"[{GetBuildingName()}] 직원이 null입니다.");
            return;
        }

        UIManager.instance.HidePanel(UIPanelType.WorkAssignment);

        // Pending Order가 있으면 그것으로 시작
        if (_pendingRecipe != null)
        {
            StartProduction(worker);
        }
        // 이미 진행 중인 Order가 있으면 직원 재할당
        else if (_currentOrder != null)
        {
            ReassignWorker(worker);
        }
    }

    /// <summary>
    /// 제작 시작 (직원 할당 완료 후)
    /// </summary>
    protected virtual void StartProduction(Employee worker)
    {
        if (_pendingRecipe == null || worker == null)
        {
            Debug.LogError($"[{GetBuildingName()}] Pending 레시피 또는 직원이 null입니다.");
            return;
        }

        // CraftingOrder 생성
        _currentOrder = new CraftingOrder(_pendingRecipe, _pendingAmount, this, worker);

        Debug.Log($"[{GetBuildingName()}] {worker.Data.employeeName}이(가) {_pendingRecipe.outputItem.itemName} x{_pendingAmount} 제작을 시작합니다.");

        // 작업자에게 작업 할당
        worker.AssignCraftingWork(_currentOrder, workPosition.position);

        // Pending 상태 초기화
        _pendingRecipe = null;
        _pendingAmount = 0;

        // 작업 시작 시 처리 (하위 클래스에서 오버라이드 가능)
        OnProductionStarted();
    }

    /// <summary>
    /// 직원 재할당
    /// </summary>
    protected virtual void ReassignWorker(Employee newWorker)
    {
        if (_currentOrder == null || newWorker == null)
        {
            Debug.LogWarning($"[{GetBuildingName()}] 재할당할 Order 또는 직원이 없습니다.");
            return;
        }

        // 기존 직원 작업 취소
        if (_currentOrder.assignedWorker != null)
        {
            _currentOrder.assignedWorker.CancelWork();
        }

        // 새 직원 할당
        _currentOrder.assignedWorker = newWorker;
        newWorker.AssignCraftingWork(_currentOrder, workPosition.position);

        Debug.Log($"[{GetBuildingName()}] {newWorker.Data.employeeName}에게 작업을 재할당했습니다.");
    }

    protected virtual void OnWorkerAssignmentClosed()
    {
        UIManager.instance.HidePanel(UIPanelType.WorkAssignment);
    }

    protected virtual void OnWorkerAssignmentCancelled()
    {
        UIManager.instance.HidePanel(UIPanelType.WorkAssignment);
        Debug.Log($"[{GetBuildingName()}] 작업자 배정이 취소되었습니다.");
    }

    /// <summary>
    /// 제작 완료 시 호출 (CraftingOrder에서 호출)
    /// </summary>
    public virtual void OnCraftingComplete(CraftingRecipe recipe, int amount)
    {
        if (recipe == null) return;

        // 아이템 인벤토리에 추가
        int totalOutput = recipe.outputAmount * amount;
        InventoryManager.instance.AddItem(recipe.outputItem, totalOutput);

        Debug.Log($"[{GetBuildingName()}] 제작 완료! {recipe.outputItem.itemName} x{totalOutput}이(가) 인벤토리에 추가되었습니다.");

        // 작업 완료 처리
        _currentOrder = null;

        // 시각 효과 제거
        StopWorkingEffect();

        // 완료 시 처리 (하위 클래스에서 오버라이드 가능)
        OnProductionCompleted(recipe, amount);
    }

    /// <summary>
    /// 제작 취소 (Order 폐기)
    /// </summary>
    public virtual void CancelProduction()
    {
        // Pending Order 취소
        if (_pendingRecipe != null)
        {
            Debug.Log($"[{GetBuildingName()}] Pending Order 취소: {_pendingRecipe.outputItem.itemName}");

            // 소모된 재료 전액 환불
            RefundMaterials(_pendingRecipe, _pendingAmount, 1f);

            _pendingRecipe = null;
            _pendingAmount = 0;
            return;
        }

        // 진행 중인 Order 취소
        if (_currentOrder == null) return;

        Debug.Log($"[{GetBuildingName()}] 제작 Order 취소: {_currentOrder.orderName}");

        // 진행도에 따른 재료 부분 환불
        RefundMaterials(_currentOrder.recipe, _currentOrder.craftAmount, _currentOrder.CraftingProgress);

        // 작업자 작업 취소
        if (_currentOrder.assignedWorker != null)
        {
            _currentOrder.assignedWorker.CancelWork();
        }

        _currentOrder.Cancel();
        _currentOrder = null;

        // 시각 효과 제거
        StopWorkingEffect();

        // 취소 시 처리 (하위 클래스에서 오버라이드 가능)
        OnProductionCancelled();
    }

    /// <summary>
    /// 재료 환불 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual void RefundMaterials(CraftingRecipe recipe, int amount, float progressRatio)
    {
        if (recipe == null || recipe.requiredMaterials == null) return;

        // 진행도에 따라 환불 비율 계산
        // 0% 진행: 100% 환불
        // 50% 진행: 50% 환불
        // 100% 진행: 0% 환불
        float refundRatio = Mathf.Clamp01(1f - progressRatio);

        foreach (var cost in recipe.requiredMaterials)
        {
            int totalAmount = cost.amount * amount;
            int refundAmount = Mathf.CeilToInt(totalAmount * refundRatio);

            if (refundAmount > 0)
            {
                InventoryManager.instance.AddItem(cost.item, refundAmount);
                Debug.Log($"[{GetBuildingName()}] {cost.item.itemName} x{refundAmount} 환불됨 (진행도: {progressRatio * 100:F0}%)");
            }
        }
    }

    /// <summary>
    /// 작업 시작 시 시각 효과 표시
    /// </summary>
    public virtual void StartWorkingEffect()
    {
        if (workingEffectPrefab == null) return;

        if (_workingEffectInstance == null)
        {
            _workingEffectInstance = Instantiate(workingEffectPrefab, transform.position + effectOffset, Quaternion.identity, transform);
        }
    }

    /// <summary>
    /// 작업 종료 시 시각 효과 제거
    /// </summary>
    public virtual void StopWorkingEffect()
    {
        if (_workingEffectInstance != null)
        {
            Destroy(_workingEffectInstance);
            _workingEffectInstance = null;
        }
    }

    protected virtual void ShowDisabledMessage()
    {
        Debug.Log($"[{GetBuildingName()}] 이 건물은 현재 사용할 수 없습니다. 기반을 복구하세요.");
        // TODO: UI 메시지 표시
    }

    // IBuildingFunction 인터페이스 구현
    public virtual void OnBuildingDisabled()
    {
        if (_currentOrder != null || _pendingRecipe != null)
        {
            CancelProduction();
            Debug.Log($"[{GetBuildingName()}] 건물이 비활성화되어 제작이 취소되었습니다.");
        }
    }

    public virtual void OnBuildingEnabled()
    {
        Debug.Log($"[{GetBuildingName()}] 건물이 다시 활성화되었습니다.");
    }

    // ===== 하위 클래스에서 오버라이드할 수 있는 훅 메서드 =====

    /// <summary>
    /// 제작 시작 시 호출 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnProductionStarted()
    {
        // 기본 구현: 시각 효과 시작
        StartWorkingEffect();
    }

    /// <summary>
    /// 제작 완료 시 호출 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        // 하위 클래스에서 추가 처리 가능
    }

    /// <summary>
    /// 제작 취소 시 호출 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnProductionCancelled()
    {
        // 하위 클래스에서 추가 처리 가능
    }

    /// <summary>
    /// 건물 이름 반환 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual string GetBuildingName()
    {
        return this.GetType().Name;
    }
}
