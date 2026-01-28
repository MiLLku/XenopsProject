using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class UIPanelData
{
    public UIPanelType type;
    public BasePanel panel;
    public bool alwaysActive; // 항상 떠 있어야 하는 경우 체크
}

public class UIManager : DestroySingleton<UIManager>
{
    [SerializeField] private List<UIPanelData> uiList;
    private Dictionary<UIPanelType, BasePanel> _uiDictionary;
    private List<BasePanel> _activePopupList = new List<BasePanel>();

    private void Start()
    {
        InitializeUIDictionary();
    }

    /// <summary>
    /// uiList에 있는 패널 데이터 리스트를 딕셔너리에 넣어줌
    /// </summary>
    private void InitializeUIDictionary()
    {
        _uiDictionary = new Dictionary<UIPanelType, BasePanel>();
        foreach (var data in uiList)
        {
            if (data.panel != null && !_uiDictionary.ContainsKey(data.type))
            {
                _uiDictionary.Add(data.type, data.panel);
                if (!data.alwaysActive)
                {
                    data.panel.gameObject.SetActive(false);
                }
            }
        }
    }

    #region 기능
    /// <summary>
    /// 특정 패널의 컴포넌트를 가져옴
    /// </summary>
    public T GetPanel<T>(UIPanelType type) where T : BasePanel
    {
        if (_uiDictionary.TryGetValue(type, out BasePanel panel))
        {
            return panel as T;
        }
        return null;
    }
    /// <summary>
    /// 특정 패널이 현재 표시 중인지 확인합니다
    /// </summary>
    public bool IsPanelActive(UIPanelType type)
    {
        if (_uiDictionary.TryGetValue(type, out BasePanel panel))
        {
            return panel.gameObject.activeSelf;
        }
        return false;
    }
    #endregion
    
    #region 조작
    /// <summary>
    /// 특정 패널을 열기
    /// </summary>
    public void ShowPanel(UIPanelType type, bool isPopup = true)
    {
        if (_uiDictionary.TryGetValue(type, out BasePanel panel))
        {
            panel.OnOpen();
            
            // 팝업이라면 관리 리스트에 추가 (중복 방지)
            if (isPopup && !_activePopupList.Contains(panel)) 
            {
                _activePopupList.Add(panel);
            }
        }
    }

    /// <summary>
    /// 특정 패널을 닫기
    /// 스택 중간에서 닫힐 경우를 처리함
    /// </summary>
    public void HidePanel(UIPanelType type)
    {
        if (_uiDictionary.TryGetValue(type, out BasePanel panel))
        {
            panel.OnClose();
        }
    }

    /// <summary>
    /// 특정 패널의 표시 상태를 토글
    /// </summary>
    public void TogglePanel(UIPanelType type, bool isPopup = true)
    {
        if (IsPanelActive(type))
        {
            HidePanel(type);
        }
        else
        {
            ShowPanel(type, isPopup);
        }
    }
    
    /// <summary>
    /// ESC 입력 시 호출될 메서드: 가장 최근에 열린 팝업 하나만 닫음
    /// </summary>
    public bool CloseTopPopup()
    {
        if (_activePopupList.Count > 0)
        {
            int lastIndex = _activePopupList.Count - 1;
            BasePanel topPanel = _activePopupList[lastIndex];
            
            topPanel.OnClose();
            _activePopupList.RemoveAt(lastIndex);
            return true;
        }
        return false;
    }
    #endregion
   
}