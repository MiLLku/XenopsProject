/// <summary>
/// 이벤트 카테고리
/// </summary>
public enum EventCategory
{
    Persistent,     // 지속형 (날씨 대체, 랜덤 기간 유지)
    Employee,       // 직원 (탈주, 질병, 갈등 등)
    Resource,       // 자원 (발견, 고갈 등)
    Visitor,        // 방문자 (상인, 이민자 등)
    Story,          // 스토리 이벤트
    Invasion,       // 침략 (적 습격 등)
    Disaster        // 재해 (화재, 붕괴 등)
}

/// <summary>
/// 이벤트 조건 타입
/// </summary>
public enum ConditionType
{
    // 직원 관련
    EmployeeCount,              // 직원 수
    EmployeeAverageHealth,      // 평균 체력
    EmployeeAverageMental,      // 평균 정신력
    EmployeeAverageHunger,      // 평균 배고픔
    EmployeeAverageFatigue,     // 평균 피로도
    EmployeeWorkingCount,       // 작업 중인 직원 수
    EmployeeIdleCount,          // 유휴 직원 수

    // 자원 관련
    InventoryItemCount,         // 특정 아이템 수량
    TotalInventoryCount,        // 전체 인벤토리 수량

    // 건물 관련
    BuildingCount,              // 건물 수
    ConstructionCount,          // 건설 중인 건물 수

    // 시간 관련
    PlayTime,                   // 플레이 시간 (초)
    DayNumber,                  // 경과 일수

    // 이벤트 관련
    ActivePersistentEvent,      // 특정 지속형 이벤트가 활성화 중인지
    EventTriggeredBefore,       // 특정 이벤트가 이전에 발생했는지

    // 기타
    RandomChance,               // 순수 확률 (0~100)
    Always                      // 항상 참
}

/// <summary>
/// 비교 연산자
/// </summary>
public enum ComparisonOperator
{
    GreaterThan,        // >
    GreaterOrEqual,     // >=
    LessThan,           // <
    LessOrEqual,        // <=
    Equal,              // ==
    NotEqual            // !=
}

/// <summary>
/// 이벤트 효과 타입
/// </summary>
public enum EffectType
{
    // 직원 스탯
    ModifyHealth,               // 체력 변경
    ModifyMental,               // 정신력 변경
    ModifyHunger,               // 배고픔 변경
    ModifyFatigue,              // 피로도 변경

    // 자원
    AddItem,                    // 아이템 추가
    RemoveItem,                 // 아이템 제거

    // 직원 관리
    SpawnEmployee,              // 직원 추가
    RemoveRandomEmployee,       // 랜덤 직원 제거 (탈주 등)

    // 건물
    DamageRandomBuilding,       // 랜덤 건물 피해
    DestroyRandomBuilding,      // 랜덤 건물 파괴

    // 맵
    DestroyRandomTiles,         // 랜덤 타일 파괴

    // 작업
    ModifyWorkSpeed,            // 작업 속도 변경 (지속형)
    PauseAllWork,               // 모든 작업 일시정지

    // 특수
    TriggerEvent,               // 다른 이벤트 발동
    EndPersistentEvent,         // 지속형 이벤트 종료

    // 메시지
    ShowNotification            // 알림 표시
}

/// <summary>
/// 효과 대상
/// </summary>
public enum EffectTarget
{
    AllEmployees,               // 모든 직원
    RandomEmployee,             // 랜덤 1명
    RandomEmployees,            // 랜덤 N명 (value로 수량 지정)
    LowestHealthEmployee,       // 체력 가장 낮은 직원
    LowestMentalEmployee,       // 정신력 가장 낮은 직원
    HighestFatigueEmployee,     // 피로도 가장 높은 직원
    WorkingEmployees,           // 작업 중인 직원들
    IdleEmployees,              // 유휴 직원들
    SpecificItem,               // 특정 아이템 (itemId 사용)
    RandomBuilding,             // 랜덤 건물
    AllBuildings,               // 모든 건물
    GlobalInventory,            // 전역 인벤토리
    Global                      // 전역 (작업 속도 등)
}
