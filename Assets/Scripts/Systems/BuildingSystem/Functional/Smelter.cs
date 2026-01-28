using UnityEngine;

/// <summary>
/// 제련소 - 광석을 제련하여 금속 주괴를 생산
/// 일정 확률로 보너스 주괴 생산 가능
/// </summary>
public class Smelter : ProductionBuilding
{
    [Header("제련소 전용")]
    [SerializeField] private ParticleSystem lavaParticles; // 용암 파티클
    [SerializeField] private Light furnaceGlow; // 화로 빛
    [SerializeField] [Range(0f, 1f)] private float bonusChance = 0.2f; // 보너스 20% 확률

    protected override string GetBuildingName()
    {
        return "제련소";
    }

    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        if (lavaParticles != null)
            lavaParticles.Play();

        if (furnaceGlow != null)
        {
            furnaceGlow.enabled = true;
            furnaceGlow.intensity = 2f;
        }

        Debug.Log("[제련소] 용광로 점화!");
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        // 보너스 체크 (확률적으로 추가 생산)
        int bonusAmount = 0;
        for (int i = 0; i < amount; i++)
        {
            if (Random.value < bonusChance)
            {
                bonusAmount++;
            }
        }

        // 기본 생산량
        int totalOutput = recipe.outputAmount * amount;

        // 보너스 생산량
        if (bonusAmount > 0)
        {
            totalOutput += recipe.outputAmount * bonusAmount;
            Debug.Log($"[제련소] ✨ 보너스 생산! +{recipe.outputAmount * bonusAmount}개 추가!");
        }

        // 인벤토리에 추가
        InventoryManager.instance.AddItem(recipe.outputItem, totalOutput);

        Debug.Log($"[제련소] 제련 완료! {recipe.outputItem.itemName} x{totalOutput}이(가) 인벤토리에 추가되었습니다.");

        // 작업 완료 처리
        _currentOrder = null;

        // 시각 효과 제거
        StopWorkingEffect();

        if (lavaParticles != null)
            lavaParticles.Stop();

        if (furnaceGlow != null)
            furnaceGlow.enabled = false;
    }

    protected override void OnProductionCancelled()
    {
        base.OnProductionCancelled();

        if (lavaParticles != null)
            lavaParticles.Stop();

        if (furnaceGlow != null)
            furnaceGlow.enabled = false;
    }
}
