using UnityEngine;

/// <summary>
/// 연금술 작업대 - 약초와 재료를 가공하여 포션을 제작하는 생산 건물
/// ProductionBuilding을 상속받아 기본 기능 사용
/// </summary>
public class AlchemyTable : ProductionBuilding
{
    [Header("연금술 작업대 전용")]
    [SerializeField] private ParticleSystem bubbleParticles; // 거품 파티클
    [SerializeField] private Color potionColor = Color.green; // 포션 색상

    protected override string GetBuildingName()
    {
        return "연금술 작업대";
    }

    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        // 거품 파티클 시작
        if (bubbleParticles != null)
        {
            var main = bubbleParticles.main;
            main.startColor = potionColor;
            bubbleParticles.Play();
        }

        Debug.Log("[연금술 작업대] 포션 제조 시작!");
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        base.OnProductionCompleted(recipe, amount);

        // 거품 파티클 정지
        if (bubbleParticles != null)
            bubbleParticles.Stop();

        Debug.Log("[연금술 작업대] 포션 제조 완료!");
    }

    // 연금술 작업대는 실패 확률이 있을 수 있음
    protected override void RefundMaterials(CraftingRecipe recipe, int amount, float progressRatio)
    {
        // 연금술은 실패 시 재료가 날아갈 수 있음 (환불률 50%)
        float refundRatio = Mathf.Clamp01((1f - progressRatio) * 0.5f);

        if (recipe == null || recipe.requiredMaterials == null) return;

        foreach (var cost in recipe.requiredMaterials)
        {
            int totalAmount = cost.amount * amount;
            int refundAmount = Mathf.CeilToInt(totalAmount * refundRatio);

            if (refundAmount > 0)
            {
                InventoryManager.instance.AddItem(cost.item, refundAmount);
                Debug.Log($"[연금술 작업대] {cost.item.itemName} x{refundAmount} 회수됨 (일부 증발)");
            }
        }
    }
}
