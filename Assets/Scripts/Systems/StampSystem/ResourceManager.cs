// --- 파일 4: ResourceManager.cs (드랍 테이블 추가 버전) ---

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ID를 Tile Asset과 연결하기 위한 구조
[System.Serializable]
public class TileEntry
{
    public int id;
    public TileBase tileAsset;
}

// ID를 Prefab과 연결하기 위한 구조
[System.Serializable]
public class EntityEntry
{
    public int id;
    public GameObject prefab;
}

// ★★★ [새로 추가된 부분 1] ★★★
// '타일 ID'를 '드랍 아이템 프리팹'과 연결하기 위한 구조
[System.Serializable]
public class DropEntry
{
    public int tileId; // 예: 1 (흙), 2 (돌)
    public GameObject dropPrefab; // 예: Dirt_Item.prefab, Stone_Item.prefab
}


[CreateAssetMenu(fileName = "ResourceManager", menuName = "StampSystem/ResourceManager")]
public class ResourceManager : ScriptableObject
{
    [Header("타일 시각 정보")]
    [SerializeField] private List<TileEntry> tileEntries;
    
    [Header("개체(건물, 식물) 프리팹")]
    [SerializeField] private List<EntityEntry> entityEntries;
    
    // ★★★ [새로 추가된 부분 2] ★★★
    [Header("타일 드랍 아이템")]
    [SerializeField] private List<DropEntry> dropEntries;

    // --- 룩업 딕셔너리 ---
    private Dictionary<int, TileBase> _tileLookup;
    private Dictionary<int, GameObject> _entityLookup;
    
    // ★★★ [새로 추가된 부분 3] ★★★
    private Dictionary<int, GameObject> _dropLookup; // 타일 ID -> 드랍 프리팹

    private void OnEnable()
    {
        // 1. 타일 룩업
        _tileLookup = new Dictionary<int, TileBase>();
        foreach (var entry in tileEntries)
        {
            _tileLookup[entry.id] = entry.tileAsset;
        }

        // 2. 개체 룩업
        _entityLookup = new Dictionary<int, GameObject>();
        foreach (var entry in entityEntries)
        {
            _entityLookup[entry.id] = entry.prefab;
        }

        // ★★★ [새로 추가된 부분 4] ★★★
        // 3. 드랍 룩업
        _dropLookup = new Dictionary<int, GameObject>();
        foreach (var entry in dropEntries)
        {
            _dropLookup[entry.tileId] = entry.dropPrefab;
        }
    }

    // --- Get 함수들 ---
    
    public TileBase GetTileAsset(int id)
    {
        _tileLookup.TryGetValue(id, out TileBase tile);
        return tile;
    }

    public GameObject GetEntityPrefab(int id)
    {
        _entityLookup.TryGetValue(id, out GameObject prefab);
        return prefab;
    }
    
    // ★★★ [새로 추가된 부분 5] ★★★
    /// <summary>
    /// 타일 ID에 해당하는 드랍 아이템 프리팹을 반환합니다.
    /// </summary>
    public GameObject GetDropPrefab(int tileId)
    {
        _dropLookup.TryGetValue(tileId, out GameObject prefab);
        return prefab; // 없으면 null 반환
    }
}