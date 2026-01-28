using UnityEngine;

/// <summary>
/// 대장간 - 광석을 가공하여 금속 부품을 제작하는 생산 건물
/// ProductionBuilding을 상속받아 기본 기능 사용
/// </summary>
public class Forge : ProductionBuilding
{
    [Header("대장간 전용")]
    [SerializeField] private ParticleSystem fireParticles; // 불꽃 파티클
    [SerializeField] private Light forgeLight; // 대장간 불빛

    protected override string GetBuildingName()
    {
        return "대장간";
    }

    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        // 불꽃 파티클 시작
        if (fireParticles != null)
            fireParticles.Play();

        // 불빛 켜기
        if (forgeLight != null)
            forgeLight.enabled = true;

        Debug.Log("[대장간] 화로에 불을 지폈습니다!");
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        base.OnProductionCompleted(recipe, amount);

        // 불꽃 파티클 정지
        if (fireParticles != null)
            fireParticles.Stop();

        // 불빛 끄기
        if (forgeLight != null)
            forgeLight.enabled = false;

        Debug.Log("[대장간] 금속 제련 완료!");
    }

    protected override void OnProductionCancelled()
    {
        base.OnProductionCancelled();

        // 불꽃 파티클 정지
        if (fireParticles != null)
            fireParticles.Stop();

        // 불빛 끄기
        if (forgeLight != null)
            forgeLight.enabled = false;
    }
}
