using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제노프스 데이터 템플릿 (ScriptableObject).
/// Unity 에디터에서 Create > StampSystem > Xenops Data 로 생성합니다.
///
/// 모든 제노프스는 최소 1개의 이로운 효과(benefits)와
/// 최소 1개의 해로운 효과(drawbacks)를 가져야 합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewXenops", menuName = "StampSystem/Xenops Data")]
public class XenopsData : ScriptableObject
{
    #region 기본 정보

    [Header("기본 정보")]
    [Tooltip("제노프스 ID (GameIDRegistry.Xenops 범위: 6000~6999)")]
    public int xenopsID;

    [Tooltip("제노프스 이름")]
    public string xenopsName;

    [TextArea(2, 4)]
    [Tooltip("제노프스 설명")]
    public string description;

    [Tooltip("아이콘")]
    public Sprite icon;

    [Tooltip("제노프스 타입")]
    public XenopsType xenopsType;

    [Tooltip("프리팹 (타입별 Behavior 컴포넌트가 부착된 상태)")]
    public GameObject prefab;

    #endregion

    #region 효과

    [Header("효과")]
    [Tooltip("이로운 효과 목록 (최소 1개)")]
    public List<XenopsEffect> benefits = new List<XenopsEffect>();

    [Tooltip("해로운 효과 목록 (최소 1개)")]
    public List<XenopsEffect> drawbacks = new List<XenopsEffect>();

    #endregion

    #region 해석 시스템

    [Header("해석 시스템")]
    [Tooltip("최대 해석도 레벨")]
    [Min(1)]
    public int maxInterpretationLevel = 10;

    [Tooltip("레벨 1→2에 필요한 기본 경험치")]
    [Min(1)]
    public int baseExpRequired = 50;

    [Tooltip("상호작용 1회당 획득 경험치")]
    [Min(1)]
    public int expPerInteraction = 10;

    #endregion

    #region 장비형 설정

    [Header("장비형 설정")]
    [Tooltip("장비 슬롯 (장비형 전용)")]
    public EquipmentSlot equipmentSlot;

    #endregion

    #region 타입별 설정

    [Header("타입별 설정")]
    [Tooltip("효과 영향 범위 (환경/적대)")]
    [Min(0)]
    public float effectRadius = 3f;

    [Tooltip("기본 공격력 (적대적 생명체)")]
    [Min(0)]
    public float hostileDamage = 5f;

    [Tooltip("공격 간격 — 초 (적대적 생명체)")]
    [Min(0.1f)]
    public float hostileAttackInterval = 5f;

    [Tooltip("자원 소모/생산 간격 — 초 (잠입체)")]
    [Min(0.1f)]
    public float infiltrateInterval = 30f;

    [Tooltip("제압 시 드랍 아이템 ID (적대적 생명체, 0 = 없음)")]
    public int subdueDropItemId;

    [Tooltip("제압 시 드랍 수량")]
    [Min(0)]
    public int subdueDropAmount = 1;

    #endregion

    #region 에디터 검증

    private void OnValidate()
    {
        if (!GameIDRegistry.Xenops.IsValid(xenopsID) && xenopsID != 0)
        {
            Debug.LogWarning($"[XenopsData] {xenopsName}: ID {xenopsID}는 Xenops 범위(6000~6999)에 속하지 않습니다!");
        }

        if (benefits.Count == 0)
        {
            Debug.LogWarning($"[XenopsData] {xenopsName}: 이로운 효과가 없습니다! 최소 1개 필요.");
        }

        if (drawbacks.Count == 0)
        {
            Debug.LogWarning($"[XenopsData] {xenopsName}: 해로운 효과가 없습니다! 최소 1개 필요.");
        }
    }

    #endregion
}
