using UnityEngine;

public abstract class BasePanel : MonoBehaviour
{
    [Header("Panel Settings")]
    public UIPanelType panelType;

    // 패널이 열릴 때 호출
    public virtual void OnOpen()
    {
        gameObject.SetActive(true);
    }

    // 패널이 닫힐 때 호출
    public virtual void OnClose()
    {
        gameObject.SetActive(false);
    }
}