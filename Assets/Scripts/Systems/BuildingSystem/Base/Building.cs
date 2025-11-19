using System.Collections.Generic;
using UnityEngine;
using System.Linq;


[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Building : MonoBehaviour
{
    [Header("건물 데이터")]
    [Tooltip("이 건물의 원본 데이터 (프리팹 인스펙터에서 수동 연결)")]
    public BuildingData buildingData; 

    [Header("기능 스크립트 (선택사항)")]
    [Tooltip("수동으로 지정할 기능 스크립트 (자동 감지와 병행 사용)")]
    [SerializeField] private List<MonoBehaviour> manualFunctionalScripts;

    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = false;

    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private int _currentHealth;
    private bool _isFunctional = true;
    
    // 자동으로 감지된 기능 컴포넌트들
    private List<IBuildingFunction> _buildingFunctions;
    private List<MonoBehaviour> _allFunctionalScripts;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        
        // IBuildingFunction을 구현한 모든 컴포넌트 자동 감지
        _buildingFunctions = GetComponents<IBuildingFunction>().ToList();
        
        // 모든 기능 스크립트 통합 (자동 감지 + 수동 지정)
        _allFunctionalScripts = new List<MonoBehaviour>();
        
        // IBuildingFunction을 구현한 스크립트들 추가
        foreach (var func in _buildingFunctions)
        {
            if (func is MonoBehaviour script)
            {
                _allFunctionalScripts.Add(script);
            }
        }
        
        // 수동으로 지정한 스크립트들 추가 (중복 제거)
        if (manualFunctionalScripts != null)
        {
            foreach (var script in manualFunctionalScripts)
            {
                if (script != null && !_allFunctionalScripts.Contains(script))
                {
                    _allFunctionalScripts.Add(script);
                }
            }
        }
        
        if (buildingData != null)
        {
            Initialize(buildingData);
        }
        else
        {
            Debug.LogError($"[Building] '{this.name}'에 BuildingData가 연결되지 않았습니다!");
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[Building] {name}: 자동 감지된 기능 {_buildingFunctions.Count}개, " +
                     $"수동 지정된 기능 {manualFunctionalScripts?.Count ?? 0}개");
        }
    }

    public void Initialize(BuildingData data)
    {
        buildingData = data;
        _currentHealth = data.maxHealth;
        _isFunctional = true;
        this.name = $"{data.buildingName} ({this.gameObject.GetInstanceID()})";
    }

    public void OnFoundationDestroyed()
    {
        if (!_isFunctional) return; 

        _isFunctional = false;
        Debug.Log($"[Building] {buildingData.buildingName}의 기반이 파괴되어 사용 불가능 상태가 됩니다.");

        // 1. 시각적 피드백: 투명도를 50%로 설정
        SetVisualState(false);
        
        // 2. 상호작용 비활성화: 콜라이더 비활성화 (클릭 방지)
        _collider.enabled = false; 
        
        // 3. 모든 기능 스크립트 비활성화
        DisableAllFunctions();
    }
    
    public void RepairFoundation()
    {
        if (_isFunctional) return;
        
        _isFunctional = true;
        Debug.Log($"[Building] {buildingData.buildingName}의 기반이 복구되어 다시 사용 가능합니다.");
        
        // 1. 시각적 피드백 복구
        SetVisualState(true);
        
        // 2. 상호작용 활성화
        _collider.enabled = true;
        
        // 3. 모든 기능 스크립트 활성화
        EnableAllFunctions();
    }
    
    private void SetVisualState(bool isActive)
    {
        Color color = _spriteRenderer.color;
        color.a = isActive ? 1.0f : 0.5f;
        _spriteRenderer.color = color;
    }
    
    private void DisableAllFunctions()
    {
        // IBuildingFunction 인터페이스를 구현한 컴포넌트들에게 알림
        foreach (var function in _buildingFunctions)
        {
            function.OnBuildingDisabled();
        }
        
        // 모든 기능 스크립트 비활성화
        Debug.Log($"[Building] {_allFunctionalScripts.Count}개의 기능 스크립트를 비활성화합니다.");
        foreach (var script in _allFunctionalScripts)
        {
            if (script != null)
            {
                script.enabled = false;
            }
        }
    }
    
    private void EnableAllFunctions()
    {
        // 모든 기능 스크립트 활성화
        Debug.Log($"[Building] {_allFunctionalScripts.Count}개의 기능 스크립트를 활성화합니다.");
        foreach (var script in _allFunctionalScripts)
        {
            if (script != null)
            {
                script.enabled = true;
            }
        }
        
        // IBuildingFunction 인터페이스를 구현한 컴포넌트들에게 알림
        foreach (var function in _buildingFunctions)
        {
            function.OnBuildingEnabled();
        }
    }
    
    public void TakeDamage(int amount)
    {
        if (!_isFunctional) return;
        
        _currentHealth -= amount;
        Debug.Log($"[Building] {buildingData.buildingName}이(가) {amount}의 피해를 받았습니다. (남은 체력: {_currentHealth}/{buildingData.maxHealth})");
        
        if (_currentHealth <= 0)
        {
            DestroyBuilding();
        }
    }
    
    private void DestroyBuilding()
    {
        Debug.Log($"[Building] {buildingData.buildingName}이(가) 파괴되었습니다!");
        
        // 건물이 차지하던 타일 점유 해제
        ReleaseOccupiedTiles();
        
        // 파괴 이펙트 (나중에 추가)
        // Instantiate(destructionEffect, transform.position, Quaternion.identity);
        
        // 일부 자원 반환 (선택사항)
        ReturnPartialResources();
        
        Destroy(gameObject);
    }
    
    private void ReleaseOccupiedTiles()
    {
        // InteractionManager를 통해 점유 해제
        if (InteractionManager.instance != null)
        {
            // 건물 위치 계산 및 점유 해제 로직
            // (InteractionManager에 메서드 추가 필요)
        }
    }
    
    private void ReturnPartialResources()
    {
        // 건설 비용의 일부를 반환 (예: 50%)
        if (InventoryManager.instance != null && buildingData != null)
        {
            foreach (var cost in buildingData.requiredResources)
            {
                int returnAmount = Mathf.Max(1, cost.amount / 2);
                InventoryManager.instance.AddItem(cost.item, returnAmount);
            }
        }
    }
    
    // 건물 상태 확인용 프로퍼티
    public bool IsFunctional => _isFunctional;
    public int CurrentHealth => _currentHealth;
    public float HealthPercentage => (float)_currentHealth / buildingData.maxHealth;
}