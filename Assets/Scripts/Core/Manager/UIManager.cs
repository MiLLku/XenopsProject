using System.Collections.Generic;
using UnityEngine;
    
    
[System.Serializable]
public class UIPanelData
{
    public UIPanelType type;
    public GameObject panelObject;
}

public class UIManager : DestroySingleton<UIManager>
{
    // 인스펙터에서 할당하기 위한 리스트
    [SerializeField] private List<UIPanelData> uiList; 
    
    // 실제 런타임에서 빠른 접근을 위해 사용할 딕셔너리
    private Dictionary<UIPanelType, GameObject> _uiDictionary;

    // 현재 열려있는 팝업 등을 추적하기 위한 스택
    private Stack<UIPanelType> _activePopupStack = new Stack<UIPanelType>();

    private void Start()
    {
        InitializeUIDictionary();
    }

    /// <summary>
    /// uiList에 있는 패널 데이터 리스트를 딕셔너리에 넣어줌
    /// </summary>
    private void InitializeUIDictionary()
    {
        _uiDictionary = new Dictionary<UIPanelType, GameObject>();
        foreach (var data in uiList)
        {
            if (!_uiDictionary.ContainsKey(data.type))
            {
                _uiDictionary.Add(data.type, data.panelObject);
                data.panelObject.SetActive(false); 
            }
        }
    }

    /// <summary>
    /// 특정 패널의 스크립트(컴포넌트)를 안전하게 가져오는 메소드
    /// </summary>
    /// <param name="type"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetPanel<T>(UIPanelType type) where T : Component
    {
        if (_uiDictionary.TryGetValue(type, out GameObject panelObj))
        {
            // 딕셔너리에 있는 오브젝트에서 해당 컴포넌트를 찾아 반환
            var component = panelObj.GetComponent<T>();
            return component;
        }
        
        return null;
    }
}