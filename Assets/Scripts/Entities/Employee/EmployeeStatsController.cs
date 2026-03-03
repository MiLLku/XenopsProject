using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원 스탯/욕구 관리 컴포넌트.
/// 4개 스탯(체력, 허기, 피로, 정신력)의 업데이트, 특성 보정, 위험 상태 체크를 담당합니다.
///
/// 욕구 흐름:
///   - 허기: 매 프레임 감소 (hungerDecayRate × 특성 보정)
///   - 피로: 작업 중 증가, 휴식 중 회복
///   - 기아(허기 0): 체력/정신력 감소
///   - 탈진(피로 0): 정신력 감소
///   - 체력 0 → Dead, 정신력 0 → MentalBreak
/// </summary>
public class EmployeeStatsController : MonoBehaviour
{
    #region 상수

    /// <summary>초기 배고픔/피로 값</summary>
    private const float INITIAL_NEEDS_VALUE = 100f;

    /// <summary>피로 < 20% 일 때 작업 속도 감소율</summary>
    private const float SEVERE_FATIGUE_SPEED = 0.5f;

    /// <summary>피로 < 50% 일 때 작업 속도 감소율</summary>
    private const float MODERATE_FATIGUE_SPEED = 0.75f;

    /// <summary>휴식 중 피로 회복 속도 (포인트/초)</summary>
    private const float REST_FATIGUE_RECOVERY = 10f;

    /// <summary>굶주림 시 체력 감소 속도 (포인트/초)</summary>
    private const float STARVATION_HEALTH_DECAY = 1f;

    /// <summary>굶주림 시 정신력 감소 속도 (포인트/초)</summary>
    private const float STARVATION_MENTAL_DECAY = 2f;

    /// <summary>탈진 시 정신력 감소 속도 (포인트/초)</summary>
    private const float EXHAUSTION_MENTAL_DECAY = 3f;

    #endregion

    #region 필드

    /// <summary>현재 스탯</summary>
    [SerializeField] private EmployeeStats currentStats;

    /// <summary>현재 욕구</summary>
    [SerializeField] private EmployeeNeeds currentNeeds;

    /// <summary>특성 효과: 체력 보정</summary>
    private float cachedHealthModifier = 1f;

    /// <summary>특성 효과: 정신력 보정</summary>
    private float cachedMentalModifier = 1f;

    /// <summary>특성 효과: 작업 속도 보정</summary>
    private float cachedWorkSpeedModifier = 1f;

    /// <summary>특성 효과: 배고픔 감소 속도 보정</summary>
    private float cachedHungerRateModifier = 1f;

    /// <summary>특성 효과: 피로 증가 속도 보정</summary>
    private float cachedFatigueRateModifier = 1f;

    /// <summary>코디네이터 참조</summary>
    private Employee employee;

    #endregion

    #region 이벤트

    public delegate void StatsChangedDelegate(EmployeeStats stats);
    public event StatsChangedDelegate OnStatsChanged;

    public delegate void NeedsChangedDelegate(EmployeeNeeds needs);
    public event NeedsChangedDelegate OnNeedsChanged;

    #endregion

    #region 프로퍼티

    /// <summary>현재 스탯</summary>
    public EmployeeStats Stats => currentStats;

    /// <summary>현재 욕구</summary>
    public EmployeeNeeds Needs => currentNeeds;

    /// <summary>캐시된 글로벌 작업 속도 보정</summary>
    public float CachedWorkSpeedModifier => cachedWorkSpeedModifier;

    #endregion

    #region 초기화

    void Awake()
    {
        employee = GetComponent<Employee>();
    }

    /// <summary>
    /// 템플릿 데이터로 스탯/욕구를 초기화합니다.
    /// </summary>
    /// <param name="data">직원 템플릿 데이터</param>
    public void Initialize(EmployeeData data)
    {
        CalculateTraitModifiers(data);

        currentStats = new EmployeeStats
        {
            health = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            maxHealth = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            mental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            maxMental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            attackPower = Mathf.RoundToInt(data.attackPower * (1f + GetTraitAttackModifier(data)))
        };

        currentNeeds = new EmployeeNeeds
        {
            hunger = INITIAL_NEEDS_VALUE,
            fatigue = INITIAL_NEEDS_VALUE
        };
    }

    #endregion

    #region 업데이트

    void Update()
    {
        if (employee == null || employee.State == EmployeeState.Dead) return;

        UpdateNeeds(Time.deltaTime);
        CheckCriticalNeeds();
    }

    /// <summary>
    /// 매 프레임 욕구(배고픔, 피로)를 갱신하고 파생 효과를 적용합니다.
    /// </summary>
    private void UpdateNeeds(float deltaTime)
    {
        EmployeeData data = employee.Data;
        if (data == null) return;

        // 허기 감소
        float hungerDecay = data.hungerDecayRate * cachedHungerRateModifier;
        currentNeeds.hunger -= hungerDecay * deltaTime;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);

        // 피로: 작업 중 증가, 휴식 중 회복
        if (employee.State == EmployeeState.Working)
        {
            float fatigueIncrease = data.fatigueIncreaseRate * cachedFatigueRateModifier;
            currentNeeds.fatigue -= fatigueIncrease * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }
        else if (employee.State == EmployeeState.Resting)
        {
            currentNeeds.fatigue += REST_FATIGUE_RECOVERY * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }

        // 기아: 체력/정신력 감소
        if (currentNeeds.hunger <= 0f)
        {
            currentStats.health -= STARVATION_HEALTH_DECAY * deltaTime;
            currentStats.mental -= STARVATION_MENTAL_DECAY * deltaTime;
        }

        // 탈진: 정신력 감소
        if (currentNeeds.fatigue <= 0f)
        {
            currentStats.mental -= EXHAUSTION_MENTAL_DECAY * deltaTime;
        }

        // 클램프
        currentStats.health = Mathf.Clamp(currentStats.health, 0f, currentStats.maxHealth);
        currentStats.mental = Mathf.Clamp(currentStats.mental, 0f, currentStats.maxMental);

        OnNeedsChanged?.Invoke(currentNeeds);
        OnStatsChanged?.Invoke(currentStats);
    }

    /// <summary>
    /// 위험 상태를 확인합니다 (체력 0 → Dead, 정신력 0 → MentalBreak).
    /// </summary>
    private void CheckCriticalNeeds()
    {
        if (currentStats.health <= 0f)
        {
            employee.SetState(EmployeeState.Dead);
            return;
        }

        if (currentStats.mental <= 0f)
        {
            employee.SetState(EmployeeState.MentalBreak);
            return;
        }
    }

    #endregion

    #region 특성 효과

    /// <summary>
    /// 모든 특성의 효과를 계산하여 캐시합니다.
    /// </summary>
    public void CalculateTraitModifiers(EmployeeData data)
    {
        cachedHealthModifier = 1f;
        cachedMentalModifier = 1f;
        cachedWorkSpeedModifier = 1f;
        cachedHungerRateModifier = 1f;
        cachedFatigueRateModifier = 1f;

        if (data?.traits == null) return;

        foreach (var trait in data.traits)
        {
            if (trait == null) continue;

            cachedHealthModifier += trait.effects.healthModifier / 100f;
            cachedMentalModifier += trait.effects.mentalModifier / 100f;
            cachedWorkSpeedModifier += trait.effects.globalWorkSpeedModifier / 100f;
            cachedHungerRateModifier += trait.effects.hungerRateModifier / 100f;
            cachedFatigueRateModifier += trait.effects.fatigueRateModifier / 100f;
        }
    }

    /// <summary>
    /// 특성에 의한 공격력 보정 계수를 반환합니다.
    /// </summary>
    private float GetTraitAttackModifier(EmployeeData data)
    {
        float modifier = 0f;
        if (data?.traits == null) return modifier;

        foreach (var trait in data.traits)
        {
            if (trait != null)
            {
                modifier += trait.effects.attackModifier / 100f;
            }
        }
        return modifier;
    }

    #endregion

    #region 공개 API

    /// <summary>
    /// 식사하여 허기를 회복합니다.
    /// </summary>
    public void Eat(float nutritionValue)
    {
        currentNeeds.hunger += nutritionValue;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        OnNeedsChanged?.Invoke(currentNeeds);
    }

    /// <summary>체력 수정</summary>
    public void ModifyHealth(float amount)
    {
        currentStats.health += amount;
        currentStats.health = Mathf.Clamp(currentStats.health, 0f, currentStats.maxHealth);
        OnStatsChanged?.Invoke(currentStats);
    }

    /// <summary>정신력 수정</summary>
    public void ModifyMental(float amount)
    {
        currentStats.mental += amount;
        currentStats.mental = Mathf.Clamp(currentStats.mental, 0f, currentStats.maxMental);
        OnStatsChanged?.Invoke(currentStats);
    }

    /// <summary>배고픔 수정</summary>
    public void ModifyHunger(float amount)
    {
        currentNeeds.hunger += amount;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        OnNeedsChanged?.Invoke(currentNeeds);
    }

    /// <summary>피로도 수정</summary>
    public void ModifyFatigue(float amount)
    {
        currentNeeds.fatigue += amount;
        currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        OnNeedsChanged?.Invoke(currentNeeds);
    }

    /// <summary>
    /// 피로도에 따른 작업 속도 감소율을 반환합니다.
    /// 피로 20% 미만: 50%, 50% 미만: 75%, 이상: 100%.
    /// </summary>
    public float GetFatigueModifier()
    {
        if (currentNeeds.fatigue < 20f) return SEVERE_FATIGUE_SPEED;
        if (currentNeeds.fatigue < 50f) return MODERATE_FATIGUE_SPEED;
        return 1f;
    }

    /// <summary>
    /// 최대 스탯을 직접 증가시킵니다 (레벨업용).
    /// </summary>
    public void IncreaseMaxStats(int healthGain, int mentalGain, int attackGain)
    {
        currentStats.maxHealth += healthGain;
        currentStats.health = currentStats.maxHealth; // 레벨업 시 체력 회복
        currentStats.maxMental += mentalGain;
        currentStats.mental = currentStats.maxMental; // 레벨업 시 정신력 회복
        currentStats.attackPower += attackGain;

        OnStatsChanged?.Invoke(currentStats);
    }

    #endregion

    #region 저장/복원

    /// <summary>
    /// 저장 데이터에 스탯/욕구 정보를 기록합니다.
    /// </summary>
    public void PopulateSaveData(EmployeeSaveData data)
    {
        data.maxHealth = (int)currentStats.maxHealth;
        data.currentHealth = (int)currentStats.health;
        data.maxMental = (int)currentStats.maxMental;
        data.currentMental = (int)currentStats.mental;
        data.attackPower = currentStats.attackPower;

        data.hunger = currentNeeds.hunger;
        data.fatigue = currentNeeds.fatigue;
    }

    /// <summary>
    /// 저장 데이터에서 스탯/욕구를 복원합니다.
    /// </summary>
    public void RestoreFromSaveData(EmployeeSaveData data)
    {
        currentStats = new EmployeeStats
        {
            maxHealth = data.maxHealth,
            health = data.currentHealth,
            maxMental = data.maxMental,
            mental = data.currentMental,
            attackPower = data.attackPower
        };

        currentNeeds = new EmployeeNeeds
        {
            hunger = data.hunger,
            fatigue = data.fatigue
        };

        // 특성 보정 캐시 재계산
        Employee emp = GetComponent<Employee>();
        if (emp?.Data != null)
        {
            CalculateTraitModifiers(emp.Data);
        }
    }

    #endregion
}
