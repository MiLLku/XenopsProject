using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEmployee", menuName = "StampSystem/Employee Data")]
public class EmployeeData : ScriptableObject
{
    [Header("기본 정보")]
    public int employeeID;
    public string employeeName;
    public Sprite portrait;
    
    [Header("기본 스탯")]
    [Range(50, 200)]
    public int maxHealth = 100;
    [Range(50, 200)]
    public int maxMental = 100;
    [Range(1, 50)]
    public int attackPower = 10;
    
    [Header("작업 능력")]
    [Tooltip("이 직원이 수행할 수 있는 작업 종류")]
    public WorkAbilities abilities;
    
    [Header("특성")]
    [Tooltip("이 직원이 가진 특성 목록")]
    public List<EmployeeTrait> traits = new List<EmployeeTrait>();
    
    [Header("기본 욕구 설정")]
    [Range(0.1f, 5f)]
    [Tooltip("배고픔이 감소하는 속도 (포인트/초)")]
    public float hungerDecayRate = 1f;
    
    [Range(0.1f, 5f)]
    [Tooltip("피로가 증가하는 속도 (포인트/초)")]
    public float fatigueIncreaseRate = 0.5f;
}

[System.Serializable]
public class WorkAbilities
{
    [Header("작업 능력 (체크된 항목만 수행 가능)")]
    public bool canMine = false;      // 채광
    public bool canChop = false;      // 벌목
    public bool canResearch = false;  // 연구
    public bool canCraft = false;     // 제작
    public bool canGarden = false;    // 원예
    public bool canBuild = false;
    public bool canHaul = false;
    public bool canDemolish = false;
    
    [Header("능력치 보정 (1.0 = 100% 속도)")]
    [Range(0.5f, 2f)]
    public float miningSpeed = 1f;
    [Range(0.5f, 2f)]
    public float choppingSpeed = 1f;
    [Range(0.5f, 2f)]
    public float researchSpeed = 1f;
    [Range(0.5f, 2f)]
    public float craftingSpeed = 1f;
    [Range(0.5f, 2f)]
    public float gardeningSpeed = 1f;
    [Range(0.5f, 2f)]
    public float buildingSpeed = 1f;
    [Range(0.5f, 2f)]
    public float haulingSpeed = 1f;
    [Range(0.5f, 2f)]
    public float demolishSpeed = 1f;
    
    public bool CanPerformWork(WorkType type)
    {
        switch (type)
        {
            case WorkType.Mining: return canMine;
            case WorkType.Chopping: return canChop;
            case WorkType.Research: return canResearch;
            case WorkType.Crafting: return canCraft;
            case WorkType.Gardening: return canGarden;
            case WorkType.Building: return canBuild;
            case WorkType.Hauling: return canHaul;
            case WorkType.Demolish: return canDemolish;
            case WorkType.Resting: return true;
            case WorkType.Eating: return true;
            default: return false;
        }
    }
    
    public float GetWorkSpeed(WorkType type)
    {
        switch (type)
        {
            case WorkType.Mining: return canMine ? miningSpeed : 0f;
            case WorkType.Chopping: return canChop ? choppingSpeed : 0f;
            case WorkType.Research: return canResearch ? researchSpeed : 0f;
            case WorkType.Crafting: return canCraft ? craftingSpeed : 0f;
            case WorkType.Gardening: return canGarden ? gardeningSpeed : 0f;
            case WorkType.Building: return canBuild ? buildingSpeed : 0f;
            case WorkType.Hauling: return canHaul ? haulingSpeed : 0f;
            case WorkType.Demolish: return canDemolish ? demolishSpeed : 0f;
            case WorkType.Resting: return 1f;
            case WorkType.Eating: return 1f;
            default: return 0f;
        }
    }
}