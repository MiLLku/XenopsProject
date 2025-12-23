using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 획득한 자원 목록을 표시하는 UI
/// </summary>
public class ResourceInventoryUI : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private GameObject panel; // UI 패널
    [SerializeField] private Transform contentContainer; // 스크롤뷰의 Content
    [SerializeField] private GameObject itemRowPrefab; // 아이템 행 프리팹
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I; // 인벤토리 열기/닫기 단축키

    private bool isOpen = false;

    void Start()
    {
        // 패널 초기 상태는 닫힘
        if (panel != null)
        {
            panel.SetActive(false);
        }

        // 닫기 버튼 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseUI);
        }

        // 인벤토리 변경 이벤트 구독
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged += OnInventoryChanged;
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= OnInventoryChanged;
        }
    }

    void Update()
    {
        // 단축키로 토글
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI();
        }
    }

    /// <summary>
    /// UI 열기/닫기 토글
    /// </summary>
    public void ToggleUI()
    {
        if (isOpen)
        {
            CloseUI();
        }
        else
        {
            OpenUI();
        }
    }

    /// <summary>
    /// UI 열기
    /// </summary>
    public void OpenUI()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            isOpen = true;
            RefreshUI();
        }
    }

    /// <summary>
    /// UI 닫기
    /// </summary>
    public void CloseUI()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            isOpen = false;
        }
    }

    /// <summary>
    /// UI 내용 갱신
    /// </summary>
    private void RefreshUI()
    {
        if (contentContainer == null || itemRowPrefab == null) return;

        // 기존 아이템 행 삭제
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // 인벤토리에서 아이템 목록 가져오기
        if (InventoryManager.instance == null) return;

        List<KeyValuePair<ItemData, int>> items = InventoryManager.instance.GetAllItems();

        // 헤더 업데이트
        if (headerText != null)
        {
            headerText.text = $"자원 목록 ({items.Count}종)";
        }

        // 아이템이 없으면 "비어있음" 표시
        if (items.Count == 0)
        {
            GameObject emptyRow = Instantiate(itemRowPrefab, contentContainer);
            TextMeshProUGUI[] texts = emptyRow.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = "비어있음";
            if (texts.Length > 1) texts[1].text = "";
            return;
        }

        // 각 아이템에 대한 행 생성
        foreach (var kvp in items)
        {
            CreateItemRow(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// 아이템 행 생성
    /// </summary>
    private void CreateItemRow(ItemData itemData, int count)
    {
        if (itemData == null) return;

        GameObject row = Instantiate(itemRowPrefab, contentContainer);

        // 아이템 이름과 개수 표시
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            texts[0].text = itemData.itemName; // 아이템 이름
        }
        if (texts.Length > 1)
        {
            texts[1].text = $"x{count}"; // 개수
        }
    }

    /// <summary>
    /// 인벤토리 변경 이벤트 핸들러
    /// </summary>
    private void OnInventoryChanged(ItemData item, int changeAmount)
    {
        // UI가 열려있을 때만 갱신
        if (isOpen)
        {
            RefreshUI();
        }
    }
}
