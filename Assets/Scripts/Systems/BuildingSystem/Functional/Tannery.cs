using UnityEngine;

/// <summary>
/// 무두질 작업장 - 가죽을 가공
/// 맑은 날씨에만 작업 가능 (햇빛 필요)
/// </summary>
public class Tannery : ProductionBuilding
{
    [Header("무두질 작업장 전용")]
    [SerializeField] private bool requiresSunlight = true; // 햇빛 필요 여부

    protected override string GetBuildingName()
    {
        return "무두질 작업장";
    }

    protected override void OnProductionOrderCreated(CraftingRecipe recipe, int amount, Employee worker)
    {
        // 날씨 체크
        if (requiresSunlight && !IsWeatherSuitable())
        {
            Debug.LogWarning("[무두질 작업장] 햇빛이 필요합니다! 비가 오거나 밤에는 작업할 수 없습니다.");

            // 재료 환불
            RefundMaterials(recipe, amount, 1f);

            // TODO: UI 메시지 표시
            return;
        }

        // 날씨가 괜찮으면 정상 진행
        base.OnProductionOrderCreated(recipe, amount, worker);
    }

    private bool IsWeatherSuitable()
    {
        // TODO: 실제 날씨 시스템과 연동
        // 현재는 임시로 항상 true

        // 예시:
        // return WeatherSystem.instance.IsDay && !WeatherSystem.instance.IsRaining;

        return true;
    }

    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        Debug.Log("[무두질 작업장] 가죽을 햇빛에 말리기 시작합니다!");
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        base.OnProductionCompleted(recipe, amount);

        Debug.Log("[무두질 작업장] 가죽 무두질 완료!");
    }
}
