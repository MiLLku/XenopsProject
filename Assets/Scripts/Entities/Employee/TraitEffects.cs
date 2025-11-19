using UnityEngine;

[System.Serializable]
public class TraitEffects
{
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
    
    [Header("작업 속도 보정")]
    [Tooltip("전체 작업 속도 보정 (%)")]
    [Range(-50, 100)]
    public float globalWorkSpeedModifier = 0;
    
    [Tooltip("특정 작업 속도 보정")]
    public WorkSpeedModifier[] specificWorkModifiers;
    
    [Header("욕구 수정")]
    [Tooltip("배고픔 감소 속도 보정 (%)")]
    [Range(-50, 50)]
    public float hungerRateModifier = 0;
    
    [Tooltip("피로 증가 속도 보정 (%)")]
    [Range(-50, 50)]
    public float fatigueRateModifier = 0;
    
    [Header("특수 효과")]
    [Tooltip("야간 작업 가능")]
    public bool canWorkAtNight = false;
    
    [Tooltip("비 오는 날 작업 불가")]
    public bool cannotWorkInRain = false;
    
    [Tooltip("혼자 작업시 효율 증가")]
    public bool lonewolfBonus = false;
    
    [Tooltip("팀 작업시 효율 증가")]
    public bool teamworkBonus = false;
}