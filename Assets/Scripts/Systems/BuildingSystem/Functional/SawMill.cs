using UnityEngine;

/// <summary>
/// 제재소 - 나무를 가공하여 목재 부품을 제작하는 생산 건물
/// ProductionBuilding을 상속받아 기본 기능 사용
/// </summary>
public class SawMill : ProductionBuilding
{
    protected override string GetBuildingName()
    {
        return "제재소";
    }

    // 필요하다면 제재소만의 특별한 동작을 오버라이드
    // 예: 톱질 소리 재생
    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        if (workingSound != null)
        {
            // 톱질 소리 재생
            // AudioSource.PlayClipAtPoint(workingSound, transform.position);
        }
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        base.OnProductionCompleted(recipe, amount);

        // 제재소만의 완료 효과 (예: 나무 조각 파티클)
        Debug.Log($"[제재소] 목재 가공 완료!");
    }
}
