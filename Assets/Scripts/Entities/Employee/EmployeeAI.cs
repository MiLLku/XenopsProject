using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원의 자율적인 행동을 관리하는 AI 컨트롤러.
/// 작업 할당은 플레이어가 수동으로 하며, AI는 긴급 욕구(배고픔, 피로, 정신력)만 처리합니다.
///
/// 긴급 욕구 처리 우선순위:
///   1. 배고픔 &lt; 20% → 음식 저장소 탐색 및 식사
///   2. 피로 &lt; 20% → 침대 탐색 및 휴식
///   3. 정신력 &lt; 30% → 휴식 공간 탐색
/// </summary>
public class EmployeeAI : MonoBehaviour
{
    #region 상수

    /// <summary>긴급 배고픔 임계값 (%)</summary>
    private const float CRITICAL_HUNGER_THRESHOLD = 20f;

    /// <summary>긴급 피로 임계값 (%)</summary>
    private const float CRITICAL_FATIGUE_THRESHOLD = 20f;

    /// <summary>긴급 정신력 비율 (최대 정신력 대비)</summary>
    private const float CRITICAL_MENTAL_RATIO = 0.3f;

    /// <summary>음식 저장소 없을 때 임시 회복량</summary>
    private const float EMERGENCY_FOOD_AMOUNT = 30f;

    /// <summary>음식 저장소 있을 때 회복량</summary>
    private const float NORMAL_FOOD_AMOUNT = 50f;

    #endregion

    #region 정적 캐시

    /// <summary>태그별 오브젝트 캐시 (모든 직원이 공유)</summary>
    private static readonly Dictionary<string, GameObject[]> _tagCache = new Dictionary<string, GameObject[]>();

    /// <summary>태그별 마지막 캐시 갱신 시각</summary>
    private static readonly Dictionary<string, float> _tagCacheTime = new Dictionary<string, float>();

    /// <summary>캐시 유효 기간 (초)</summary>
    private const float TAG_CACHE_DURATION = 1f;

    /// <summary>
    /// 태그로 오브젝트를 검색합니다 (캐시 사용).
    /// 동일 프레임/짧은 간격 내 여러 직원이 동일 태그를 조회해도 1회만 실제 검색합니다.
    /// </summary>
    private static GameObject[] FindWithTagCached(string tag)
    {
        float now = Time.time;
        if (_tagCache.TryGetValue(tag, out var cached) &&
            _tagCacheTime.TryGetValue(tag, out float cachedTime) &&
            now - cachedTime < TAG_CACHE_DURATION)
        {
            return cached;
        }

        var result = GameObject.FindGameObjectsWithTag(tag);
        _tagCache[tag] = result;
        _tagCacheTime[tag] = now;
        return result;
    }

    #endregion

    #region 필드

    [Header("AI 설정")]
    [SerializeField] private float decisionInterval = 2f;
    [SerializeField] private bool enableAutonomousBehavior = true;

    /// <summary>마지막 결정 시각</summary>
    private float lastDecisionTime;

    /// <summary>소유 직원 참조</summary>
    private Employee employee;

    /// <summary>이동 컴포넌트 참조 (캐시)</summary>
    private EmployeeMovement movement;

    #endregion

    #region 초기화

    void Awake()
    {
        employee = GetComponent<Employee>();
        movement = GetComponent<EmployeeMovement>();
    }

    /// <summary>
    /// 정적 캐시를 초기화합니다 (도메인 리로드/씬 전환 시).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStaticCache()
    {
        _tagCache.Clear();
        _tagCacheTime.Clear();
    }

    #endregion

    #region 업데이트

    void Update()
    {
        if (!enableAutonomousBehavior) return;
        if (employee == null) return;

        if (Time.time - lastDecisionTime < decisionInterval) return;

        lastDecisionTime = Time.time;

        CheckCriticalNeeds();
    }

    #endregion

    #region 긴급 욕구 처리

    /// <summary>
    /// 긴급 욕구를 확인하고 필요 시 자율 행동을 수행합니다.
    /// </summary>
    private void CheckCriticalNeeds()
    {
        if (employee.Needs.hunger < CRITICAL_HUNGER_THRESHOLD && employee.State != EmployeeState.Eating)
        {
            HandleHunger();
            return;
        }

        if (employee.Needs.fatigue < CRITICAL_FATIGUE_THRESHOLD && employee.State != EmployeeState.Resting)
        {
            HandleFatigue();
            return;
        }

        if (employee.Stats.mental < employee.Stats.maxMental * CRITICAL_MENTAL_RATIO &&
            employee.State != EmployeeState.Resting &&
            employee.State != EmployeeState.MentalBreak)
        {
            HandleLowMental();
            return;
        }
    }

    /// <summary>
    /// 배고픔 처리: 가장 가까운 음식 저장소로 이동하여 식사합니다.
    /// </summary>
    private void HandleHunger()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}이(가) 배고픕니다. (배고픔: {employee.Needs.hunger:F0}%)");

        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }

        GameObject[] foodStorages = FindWithTagCached("FoodStorage");
        if (foodStorages.Length > 0)
        {
            GameObject nearest = foodStorages
                .Where(f => f != null)
                .OrderBy(f => Vector2.Distance(transform.position, f.transform.position))
                .FirstOrDefault();

            if (nearest != null && movement != null)
            {
                employee.SetState(EmployeeState.Eating);
                movement.MoveTo(nearest.transform.position, () =>
                {
                    employee.Eat(NORMAL_FOOD_AMOUNT);
                    employee.SetState(EmployeeState.Idle);
                    Debug.Log($"[AI] {employee.Data.employeeName}이(가) 식사를 완료했습니다.");
                });
            }
        }
        else
        {
            Debug.LogWarning($"[AI] 음식 저장소를 찾을 수 없습니다. {employee.Data.employeeName}이(가) 임시로 회복합니다.");
            employee.Eat(EMERGENCY_FOOD_AMOUNT);
        }
    }

    /// <summary>
    /// 피로 처리: 가장 가까운 침대로 이동하여 휴식합니다.
    /// </summary>
    private void HandleFatigue()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}이(가) 피곤합니다. (피로: {employee.Needs.fatigue:F0}%)");

        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }

        MoveToNearestTaggedObject("Bed", "침대");
    }

    /// <summary>
    /// 정신력 저하 처리: 가장 가까운 휴식 공간으로 이동합니다.
    /// </summary>
    private void HandleLowMental()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}의 정신력이 낮습니다. (정신력: {employee.Stats.mental:F0}/{employee.Stats.maxMental})");

        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }

        MoveToNearestTaggedObject("Bed", "휴식 공간");
    }

    /// <summary>
    /// 지정한 태그를 가진 가장 가까운 오브젝트로 이동합니다.
    /// </summary>
    /// <param name="tag">검색할 태그</param>
    /// <param name="displayName">로그 출력용 이름</param>
    private void MoveToNearestTaggedObject(string tag, string displayName)
    {
        GameObject[] objects = FindWithTagCached(tag);
        if (objects.Length > 0)
        {
            GameObject nearest = objects
                .Where(o => o != null)
                .OrderBy(o => Vector2.Distance(transform.position, o.transform.position))
                .FirstOrDefault();

            if (nearest != null && movement != null)
            {
                employee.SetState(EmployeeState.Resting);
                movement.MoveTo(nearest.transform.position, () =>
                {
                    Debug.Log($"[AI] {employee.Data.employeeName}이(가) {displayName}에 도착했습니다.");
                    // Resting 상태 유지 → StatsController에서 피로 회복
                });
            }
        }
        else
        {
            Debug.LogWarning($"[AI] {displayName}를 찾을 수 없습니다.");
        }
    }

    #endregion

    #region 공개 API

    /// <summary>
    /// 자율 행동 활성화/비활성화를 설정합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAutonomousBehavior(bool enabled)
    {
        enableAutonomousBehavior = enabled;

        if (!enabled)
        {
            Debug.Log($"[AI] {employee?.Data?.employeeName}의 자율 행동이 비활성화되었습니다.");
        }
    }

    #endregion

    #region 디버그

    /// <summary>
    /// AI 상태를 콘솔에 출력합니다.
    /// </summary>
    [ContextMenu("Print AI Status")]
    public void PrintAIStatus()
    {
        if (employee == null)
        {
            Debug.Log("[AI] Employee 컴포넌트가 없습니다.");
            return;
        }

        Debug.Log($"=== {employee.Data.employeeName} AI 상태 ===");
        Debug.Log($"자율 행동: {(enableAutonomousBehavior ? "활성화" : "비활성화")}");
        Debug.Log($"배고픔: {employee.Needs.hunger:F0}%");
        Debug.Log($"피로: {employee.Needs.fatigue:F0}%");
        Debug.Log($"정신력: {employee.Stats.mental:F0}/{employee.Stats.maxMental}");
        Debug.Log($"현재 상태: {employee.State}");
    }

    #endregion
}
