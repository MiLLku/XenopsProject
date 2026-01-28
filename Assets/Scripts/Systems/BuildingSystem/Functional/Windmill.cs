using UnityEngine;

/// <summary>
/// 풍차 - 곡물을 가공하여 밀가루를 제작하는 생산 건물
/// 날개가 회전하는 애니메이션이 있음
/// </summary>
public class Windmill : ProductionBuilding
{
    [Header("풍차 전용")]
    [SerializeField] private Transform windmillBlades; // 풍차 날개
    [SerializeField] private float rotationSpeed = 30f; // 회전 속도 (도/초)
    [SerializeField] private Animator windmillAnimator; // 풍차 애니메이터

    private bool _isRotating = false;

    protected override string GetBuildingName()
    {
        return "풍차";
    }

    void Update()
    {
        // 작업 중일 때만 날개 회전
        if (_isRotating && windmillBlades != null)
        {
            windmillBlades.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        }
    }

    protected override void OnProductionStarted()
    {
        base.OnProductionStarted();

        // 날개 회전 시작
        _isRotating = true;

        // 애니메이터 트리거
        if (windmillAnimator != null)
        {
            windmillAnimator.SetBool("IsWorking", true);
        }

        Debug.Log("[풍차] 풍차가 돌기 시작합니다!");
    }

    protected override void OnProductionCompleted(CraftingRecipe recipe, int amount)
    {
        base.OnProductionCompleted(recipe, amount);

        // 날개 회전 정지
        _isRotating = false;

        if (windmillAnimator != null)
        {
            windmillAnimator.SetBool("IsWorking", false);
        }

        Debug.Log("[풍차] 밀가루 제분 완료!");
    }

    protected override void OnProductionCancelled()
    {
        base.OnProductionCancelled();

        // 날개 회전 정지
        _isRotating = false;

        if (windmillAnimator != null)
        {
            windmillAnimator.SetBool("IsWorking", false);
        }
    }
}
