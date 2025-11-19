using UnityEngine;

[CreateAssetMenu(fileName = "NewTrait", menuName = "StampSystem/Employee Trait")]
public class EmployeeTrait : ScriptableObject
{
    [Header("특성 정보")]
    public string traitName;
    public string description;
    public Sprite icon;
    
    [Header("특성 타입")]
    public TraitType type;
    
    [Header("효과")]
    public TraitEffects effects;
}

[System.Serializable]
public struct WorkSpeedModifier
{
    public WorkType workType;
    [Range(-50, 100)]
    public float speedModifier;
}

public static class TraitFactory
{
    public static class TraitNames
    {
        // 긍정적 특성
        public const string HARDWORKER = "근면함";
        public const string NIGHT_OWL = "야행성";
        public const string EFFICIENT = "효율적";
        public const string STRONG = "강인함";
        public const string FOCUSED = "집중력";
        
        // 부정적 특성
        public const string LAZY = "게으름";
        public const string FRAGILE = "허약함";
        public const string CLUMSY = "서투름";
        public const string GLUTTON = "대식가";
        
        // 중립적 특성
        public const string LONEWOLF = "외톨이";
        public const string TEAMPLAYER = "팀플레이어";
    }
    
    public static TraitEffects GetHardworkerEffects()
    {
        return new TraitEffects
        {
            globalWorkSpeedModifier = 20f,
            fatigueRateModifier = 10f // 더 빨리 피곤해짐
        };
    }
    
    public static TraitEffects GetLazyEffects()
    {
        return new TraitEffects
        {
            globalWorkSpeedModifier = -20f,
            fatigueRateModifier = -10f // 덜 피곤해짐
        };
    }
    
    public static TraitEffects GetGluttonEffects()
    {
        return new TraitEffects
        {
            hungerRateModifier = 50f, // 배고픔이 빨리 감소
            healthModifier = 10f // 대신 체력은 높음
        };
    }
}


