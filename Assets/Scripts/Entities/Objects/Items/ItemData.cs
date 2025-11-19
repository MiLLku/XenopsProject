using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "StampSystem/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("아이템 기본 정보")]
    public int itemID;
    public string itemName;
    public Sprite itemIcon;
}