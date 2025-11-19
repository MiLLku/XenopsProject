using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "StampLibrary", menuName = "StampSystem/StampLibrary")]
public class StampLibrary : ScriptableObject
{
    // 1. Unity 인스펙터에서 편집하고 저장하기 위한 리스트
    [SerializeField]
    private List<StampData> stampList = new List<StampData>();

    // 2. 게임 실행 중 스탬프를 Key로 빠르게 찾기 위한 딕셔너리 (런타임용)
    private Dictionary<string, StampData> _stampLookup;

    // 3. ScriptableObject가 활성화될 때 (게임 시작 시 등) 호출됨
    private void OnEnable()
    {
        // 리스트를 딕셔너리로 변환하여 검색 속도 최적화
        _stampLookup = new Dictionary<string, StampData>();
        foreach (var stamp in stampList)
        {
            if (stamp != null && !string.IsNullOrEmpty(stamp.key))
            {
                _stampLookup[stamp.key] = stamp;
            }
        }
    }

    /// <summary>
    /// Key를 이용해 스탬프 데이터를 즉시 찾습니다.
    /// </summary>
    public StampData GetStamp(string key)
    {
        _stampLookup.TryGetValue(key, out StampData data);
        return data; // 찾지 못하면 null 반환
    }

    /// <summary>
    /// (에디터용) 라이브러리의 모든 스탬프 리스트를 반환합니다.
    /// </summary>
    public List<StampData> GetAllStamps()
    {
        return stampList;
    }

    /// <summary>
    /// (에디터용) 스탬프 리스트를 통째로 교체합니다.
    /// </summary>
    public void SetStamps(List<StampData> stamps)
    {
        stampList = stamps;
        OnEnable(); // 딕셔너리 갱신
    }
}
