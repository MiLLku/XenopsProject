using UnityEngine;
using System.Collections;

/// <summary>
/// 제작 작업 명령 - 생산 건물에서 아이템을 제작하는 작업
/// WorkOrder를 상속하고 IWorkTarget을 구현하여 WorkTask 시스템과 연동
/// </summary>
[System.Serializable]
public class CraftingOrder : WorkOrder, IWorkTarget
{
    [Header("제작 정보")]
    public CraftingRecipe recipe;           // 제작할 레시피
    public int craftAmount;                 // 제작 개수
    public ProductionBuilding building;     // 작업 건물 (생산 건물)
    public Employee assignedWorker;         // 할당된 직원

    [Header("진행 상태")]
    private float _craftingProgress = 0f;   // 제작 진행도 (0~1)
    private float _totalTime;               // 총 제작 시간
    private bool _isWorking = false;        // 작업 중 여부
    private Vector3 _workPosition;          // 작업 위치

    /// <summary>
    /// CraftingOrder 생성자
    /// </summary>
    public CraftingOrder(CraftingRecipe recipe, int amount, ProductionBuilding productionBuilding, Employee worker)
    {
        this.recipe = recipe;
        this.craftAmount = amount;
        this.building = productionBuilding;
        this.assignedWorker = worker;

        // WorkOrder 기본 설정
        this.workType = WorkType.Crafting;
        this.orderName = $"{recipe.outputItem.itemName} 제작";
        this.priority = 5;
        this.maxAssignedWorkers = 1; // 제작은 1명만
        this.isActive = true;
        this.isPaused = false;

        // 총 제작 시간 계산
        this._totalTime = recipe.craftingTime * amount;
        this._workPosition = productionBuilding.WorkPosition;

        // WorkTask 생성 (this를 IWorkTarget으로 전달)
        WorkTask task = new WorkTask(this, priority);
        taskQueue.Enqueue(task);

        // 직원을 작업물에 등록
        AssignWorker(worker);

        Debug.Log($"[CraftingOrder] 생성됨: {orderName}, 총 시간: {_totalTime}초, 작업 위치: {_workPosition}");
    }

    // ===== IWorkTarget 인터페이스 구현 =====

    /// <summary>
    /// 작업 위치 반환 (건물 위치)
    /// </summary>
    public Vector3 GetWorkPosition()
    {
        return _workPosition;
    }

    /// <summary>
    /// 작업 타입 반환
    /// </summary>
    public WorkType GetWorkType()
    {
        return WorkType.Crafting;
    }

    /// <summary>
    /// 작업 시간 반환
    /// </summary>
    public float GetWorkTime()
    {
        return _totalTime;
    }

    /// <summary>
    /// 작업 가능 여부
    /// </summary>
    public bool IsWorkAvailable()
    {
        return isActive && !isPaused && building != null && assignedWorker != null;
    }

    /// <summary>
    /// 작업 완료 처리
    /// </summary>
    public void CompleteWork(Employee worker)
    {
        if (worker != assignedWorker)
        {
            Debug.LogWarning($"[CraftingOrder] 다른 직원이 작업을 완료하려 했습니다: {worker.Data.employeeName}");
            return;
        }

        Debug.Log($"[CraftingOrder] 작업 완료: {orderName}");

        // 제작 완료 처리
        _isWorking = false;
        _craftingProgress = 1f;

        // 건물에 제작 완료 알림
        if (building != null)
        {
            building.OnCraftingComplete(recipe, craftAmount);
        }

        // WorkOrder 완료 처리
        if (taskQueue.AssignedTasks.Count > 0)
        {
            CompleteTask(taskQueue.AssignedTasks[0]);
        }
    }

    /// <summary>
    /// 작업 취소 처리
    /// </summary>
    public void CancelWork(Employee worker)
    {
        Debug.Log($"[CraftingOrder] 작업 취소: {orderName}");
        _isWorking = false;
    }

    // ===== 제작 진행 관리 =====

    /// <summary>
    /// 작업 시작
    /// </summary>
    public void StartWorking()
    {
        if (_isWorking)
        {
            Debug.LogWarning("[CraftingOrder] 이미 작업 중입니다.");
            return;
        }

        _isWorking = true;
        Debug.Log($"[CraftingOrder] 작업 시작: {orderName}");

        // 건물의 작업 효과 시작
        if (building != null)
        {
            building.StartWorkingEffect();
        }
    }

    /// <summary>
    /// 매 프레임 진행도 업데이트 (Employee.Update에서 호출)
    /// </summary>
    public void UpdateProgress(float deltaTime)
    {
        if (!_isWorking || _totalTime <= 0) return;

        _craftingProgress += deltaTime / _totalTime;

        // 진행도가 100%에 도달하면 자동 완료
        if (_craftingProgress >= 1f)
        {
            _craftingProgress = 1f;
            CompleteWork(assignedWorker);
        }
    }

    /// <summary>
    /// 제작 진행도 반환 (0~1)
    /// </summary>
    public float CraftingProgress => _craftingProgress;

    /// <summary>
    /// 작업 중 여부
    /// </summary>
    public bool IsWorking => _isWorking;

    /// <summary>
    /// 남은 시간 반환
    /// </summary>
    public float RemainingTime => _totalTime * (1f - _craftingProgress);

    // ===== 디버그 =====

    /// <summary>
    /// 디버그 정보 반환
    /// </summary>
    public new string GetDebugInfo()
    {
        string baseInfo = base.GetDebugInfo();
        string craftingInfo = $" | Crafting: {recipe?.outputItem.itemName ?? "null"} x{craftAmount} | " +
                              $"Progress: {_craftingProgress * 100:F0}% | Working: {_isWorking}";

        return baseInfo + craftingInfo;
    }
}
