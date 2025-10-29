using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps; // Tilemap을 사용하기 위해 필요

namespace StampSystem
{
    // ID를 Tile Asset과 연결하기 위한 간단한 구조
    [System.Serializable]
    public class TileEntry
    {
        public int id;
        public TileBase tileAsset; // 실제 유니티 Tilemap에 칠해질 타일 애셋
    }

    // ID를 Prefab과 연결하기 위한 구조
    [System.Serializable]
    public class EntityEntry
    {
        public int id;
        public GameObject prefab;
    }

    [CreateAssetMenu(fileName = "ResourceManager", menuName = "StampSystem/ResourceManager")]
    public class ResourceManager : ScriptableObject
    {
        // 인스펙터에서 타일과 ID를 연결합니다.
        [SerializeField] private List<TileEntry> tileEntries;
        
        // 인스펙터에서 개체(건물, 적)와 ID를 연결합니다.
        [SerializeField] private List<EntityEntry> entityEntries;

        // 빠른 검색을 위한 딕셔너리 (런타임용)
        private Dictionary<int, TileBase> _tileLookup;
        private Dictionary<int, GameObject> _entityLookup;

        // 게임 시작 시 룩업 딕셔너리 초기화
        private void OnEnable()
        {
            _tileLookup = new Dictionary<int, TileBase>();
            foreach (var entry in tileEntries)
            {
                _tileLookup[entry.id] = entry.tileAsset;
            }

            _entityLookup = new Dictionary<int, GameObject>();
            foreach (var entry in entityEntries)
            {
                _entityLookup[entry.id] = entry.prefab;
            }
        }

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
    }
}