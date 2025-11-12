using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Building : MonoBehaviour
{
    [Header("건물 데이터")]
    [Tooltip("이 건물의 원본 데이터 (프리팹 인스펙터에서 수동 연결)")]
    public BuildingData buildingData; 

    // ★★★ [새로 추가된 부분] ★★★
    [Header("기능 스크립트")]
    [Tooltip("건물이 파괴/비활성화될 때 함께 중지시킬 기능 스크립트 목록 (예: CraftingStation)")]
    [SerializeField] private List<MonoBehaviour> functionalScripts;
    // ★★★ [추가 끝] ★★★

    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private int _currentHealth;
    private bool _isFunctional = true;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        
        if (buildingData != null)
        {
            Initialize(buildingData);
        }
        else
        {
            Debug.LogError($"[Building] '{this.name}'에 BuildingData가 연결되지 않았습니다!");
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

        // 1. 투명도를 50%로 설정
        Color color = _spriteRenderer.color;
        color.a = 0.5f; 
        _spriteRenderer.color = color;
        
        // 2. 콜라이더 비활성화 (클릭 방지)
        _collider.enabled = false; 
        
        // ★★★ [수정된 로직] ★★★
        // 3. 인스펙터에 등록된 '모든' 기능 스크립트를 비활성화
        Debug.Log($"[Building] {functionalScripts.Count}개의 기능 스크립트를 비활성화합니다.");
        foreach (var script in functionalScripts)
        {
            if (script != null)
            {
                // CraftingStation이든 Smelter든 상관없이 모두 비활성화
                script.enabled = false;
            }
        }
        // ★★★ [로직 끝] ★★★
    }
    
    public void TakeDamage(int amount)
    {
        // ... (TakeDamage 로직은 동일) ...
    }
}