using UnityEngine;

/// <summary>
/// 직원 특성의 효과 데이터.
/// 스탯 보정, 작업 속도 보정, 욕구 보정, 특수 효과를 정의합니다.
/// 모든 수치는 퍼센트(%) 단위로 기본값 0 = 보정 없음.
/// </summary>
[System.Serializable]
public class TraitEffects
{
    #region 스탯 수정

    [Header("스탯 수정")]
    [Tooltip("최대 체력 보정 (%)")]
    [Range(-50, 50)]
    public float healthModifier = 0;

    [Tooltip("최대 정신력 보정 (%)")]
    [Range(-50, 50)]
    public float mentalModifier = 0;

    [Tooltip("공격력 보정 (%)")]
    [Range(-50, 50)]
    public float attackModifier = 0;

    #endregion

    #region 작업 속도 보정

    [Header("작업 속도 보정")]
    [Tooltip("전체 작업 속도 보정 (%)")]
    [Range(-50, 100)]
    public float globalWorkSpeedModifier = 0;

    [Tooltip("특정 작업 속도 보정")]
    public WorkSpeedModifier[] specificWorkModifiers;

    #endregion

    #region 욕구 수정

    [Header("욕구 수정")]
    [Tooltip("배고픔 감소 속도 보정 (%)")]
    [Range(-50, 50)]
    public float hungerRateModifier = 0;

    [Tooltip("피로 증가 속도 보정 (%)")]
    [Range(-50, 50)]
    public float fatigueRateModifier = 0;

    #endregion

    #region 특수 효과

    [Header("특수 효과")]
    [Tooltip("야간 작업 가능")]
    public bool canWorkAtNight = false;

    [Tooltip("비 오는 날 작업 불가")]
    public bool cannotWorkInRain = false;

    [Tooltip("혼자 작업시 효율 증가")]
    public bool lonewolfBonus = false;

    [Tooltip("팀 작업시 효율 증가")]
    public bool teamworkBonus = false;

    #endregion
}
