using System;
using System.Collections.Generic;

/// <summary>
/// 직원 저장 데이터
///
/// 직원 유형:
/// - 유니크 직원: isUnique=true, 템플릿 기반 + 성장 시스템
/// - 일반 직원: isUnique=false, 랜덤 생성 (추후 구현)
/// </summary>
[Serializable]
public class EmployeeSaveData
{
    // ===== 식별 =====
    public int instanceId;          // 런타임 고유 ID
    public int templateId;          // EmployeeData ScriptableObject ID (외형/프리팹용)
    public bool isUnique;           // 유니크 직원 여부

    // ===== 기본 정보 =====
    public string customName;       // 커스텀 이름 (null이면 템플릿 이름 사용)

    // ===== 위치 =====
    public float posX;
    public float posY;

    // ===== 상태 =====
    public int state;               // EmployeeState enum

    // ===== 성장 시스템 (유니크 직원 전용) =====
    public int level;
    public int experience;
    public int experienceToNextLevel;

    // ===== 현재 스탯 (성장 반영된 실제 값) =====
    public int maxHealth;           // 템플릿 기본값 + 성장치
    public int currentHealth;
    public int maxMental;
    public int currentMental;
    public int attackPower;

    // ===== 욕구 =====
    public float hunger;
    public float fatigue;

    // ===== 작업 능력 (성장으로 변경 가능) =====
    public WorkAbilitiesSaveData abilities;

    // ===== 작업 설정 =====
    public List<WorkPrioritySaveData> workPriorities;

    // ===== 현재 작업 (연결용) =====
    public int assignedWorkOrderId; // -1이면 없음

    public EmployeeSaveData()
    {
        assignedWorkOrderId = -1;
        workPriorities = new List<WorkPrioritySaveData>();
        level = 1;
        experience = 0;
        experienceToNextLevel = 100;
    }
}

/// <summary>
/// 작업 능력 저장 데이터
/// 유니크 직원은 성장에 따라 변경될 수 있음
/// </summary>
[Serializable]
public class WorkAbilitiesSaveData
{
    // 작업 가능 여부
    public bool canMine;
    public bool canChop;
    public bool canResearch;
    public bool canCraft;
    public bool canGarden;
    public bool canBuild;
    public bool canHaul;
    public bool canDemolish;

    // 작업 속도 (성장으로 향상 가능)
    public float miningSpeed;
    public float choppingSpeed;
    public float researchSpeed;
    public float craftingSpeed;
    public float gardeningSpeed;
    public float buildingSpeed;
    public float haulingSpeed;
    public float demolishSpeed;

    public WorkAbilitiesSaveData()
    {
        miningSpeed = 1f;
        choppingSpeed = 1f;
        researchSpeed = 1f;
        craftingSpeed = 1f;
        gardeningSpeed = 1f;
        buildingSpeed = 1f;
        haulingSpeed = 1f;
        demolishSpeed = 1f;
    }

    /// <summary>
    /// WorkAbilities에서 복사
    /// </summary>
    public static WorkAbilitiesSaveData FromWorkAbilities(WorkAbilities source)
    {
        if (source == null) return new WorkAbilitiesSaveData();

        return new WorkAbilitiesSaveData
        {
            canMine = source.canMine,
            canChop = source.canChop,
            canResearch = source.canResearch,
            canCraft = source.canCraft,
            canGarden = source.canGarden,
            canBuild = source.canBuild,
            canHaul = source.canHaul,
            canDemolish = source.canDemolish,
            miningSpeed = source.miningSpeed,
            choppingSpeed = source.choppingSpeed,
            researchSpeed = source.researchSpeed,
            craftingSpeed = source.craftingSpeed,
            gardeningSpeed = source.gardeningSpeed,
            buildingSpeed = source.buildingSpeed,
            haulingSpeed = source.haulingSpeed,
            demolishSpeed = source.demolishSpeed
        };
    }

    /// <summary>
    /// WorkAbilities로 변환
    /// </summary>
    public WorkAbilities ToWorkAbilities()
    {
        return new WorkAbilities
        {
            canMine = canMine,
            canChop = canChop,
            canResearch = canResearch,
            canCraft = canCraft,
            canGarden = canGarden,
            canBuild = canBuild,
            canHaul = canHaul,
            canDemolish = canDemolish,
            miningSpeed = miningSpeed,
            choppingSpeed = choppingSpeed,
            researchSpeed = researchSpeed,
            craftingSpeed = craftingSpeed,
            gardeningSpeed = gardeningSpeed,
            buildingSpeed = buildingSpeed,
            haulingSpeed = haulingSpeed,
            demolishSpeed = demolishSpeed
        };
    }
}

/// <summary>
/// 작업 우선순위 저장 데이터
/// </summary>
[Serializable]
public class WorkPrioritySaveData
{
    public int workType;    // WorkType enum
    public int priority;    // 1-9, 0=비활성
    public bool enabled;
}
