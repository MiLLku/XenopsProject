using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// 생산 건물 UI - 제작 가능한 레시피 리스트 표시 및 제작 시작
/// RecipeItem 프리팹이 각자의 수량 조절 버튼과 제작 버튼을 가짐
/// </summary>
public class ProductionUI : MonoBehaviour
{
    [Header("UI 요소 연결")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private Transform recipeListContainer; // 레시피 목록이 생성될 부모 (Content)
    [SerializeField] private GameObject recipeItemPrefab; // 레시피 아이템 프리팹
    [SerializeField] private Button closeButton;

    private List<CraftingRecipe> _availableRecipes;
    private Action<CraftingRecipe, int, Employee> _onStartProductionCallback;
    private MonoBehaviour _sourceBuilding; // 호출한 건물 참조

    // 각 레시피별 수량 저장 (RecipeItem별로 독립적)
    private Dictionary<CraftingRecipe, int> _recipeCraftAmounts = new Dictionary<CraftingRecipe, int>();
    private Dictionary<CraftingRecipe, RecipeItemUI> _recipeItemUIs = new Dictionary<CraftingRecipe, RecipeItemUI>();

    void Awake()
    {
        // 버튼 리스너 연결
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    void OnEnable()
    {
        // UI가 활성화될 때마다 수량 초기화
        _recipeCraftAmounts.Clear();
        _recipeItemUIs.Clear();
    }

    void OnDisable()
    {
        // UI 닫힐 때 정리
        ClearRecipeList();
        _recipeCraftAmounts.Clear();
        _recipeItemUIs.Clear();
    }

    /// <summary>
    /// 생산 건물에서 호출하여 UI를 초기화합니다
    /// </summary>
    public void Setup(List<CraftingRecipe> recipes, Action<CraftingRecipe, int, Employee> onStartProduction, MonoBehaviour sourceBuilding = null)
    {
        _availableRecipes = recipes;
        _onStartProductionCallback = onStartProduction;
        _sourceBuilding = sourceBuilding;

        if (headerText != null)
        {
            string buildingName = sourceBuilding != null ? sourceBuilding.name : "생산 건물";
            headerText.text = $"{buildingName} - 제작";
        }

        // 레시피별 초기 수량 설정
        _recipeCraftAmounts.Clear();
        _recipeItemUIs.Clear();
        foreach (var recipe in recipes)
        {
            if (recipe != null)
                _recipeCraftAmounts[recipe] = 1;
        }

        UpdateRecipeList();
    }

    /// <summary>
    /// 레시피 리스트를 갱신합니다
    /// </summary>
    private void UpdateRecipeList()
    {
        ClearRecipeList();

        if (_availableRecipes == null || _availableRecipes.Count == 0)
        {
            Debug.LogWarning("[ProductionUI] 사용 가능한 레시피가 없습니다.");
            return;
        }

        foreach (var recipe in _availableRecipes)
        {
            if (recipe == null) continue;

            CreateRecipeItem(recipe);
        }
    }

    /// <summary>
    /// 기존 레시피 목록을 정리합니다
    /// </summary>
    private void ClearRecipeList()
    {
        if (recipeListContainer == null) return;

        foreach (Transform child in recipeListContainer)
        {
            Destroy(child.gameObject);
        }

        _recipeItemUIs.Clear();
    }

    /// <summary>
    /// 레시피 아이템을 생성합니다
    /// </summary>
    private void CreateRecipeItem(CraftingRecipe recipe)
    {
        GameObject item = Instantiate(recipeItemPrefab, recipeListContainer);

        // RecipeItemUI 헬퍼 클래스 생성
        RecipeItemUI itemUI = new RecipeItemUI();

        // UI 요소 찾기
        itemUI.nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        itemUI.iconImage = item.transform.Find("Image")?.GetComponent<Image>();
        itemUI.minusButton = item.transform.Find("MinusButton")?.GetComponent<Button>();
        itemUI.plusButton = item.transform.Find("PlusButton")?.GetComponent<Button>();
        itemUI.makeOrderButton = item.transform.Find("MakeProductionOrder")?.GetComponent<Button>();
        itemUI.amountText = item.transform.Find("MinusButton")?.parent?.Find("AmountText")?.GetComponent<TextMeshProUGUI>();

        // AmountText가 다른 위치에 있을 수 있으므로 추가 검색
        if (itemUI.amountText == null)
        {
            // NameText가 아닌 다른 TextMeshProUGUI 찾기
            var allTexts = item.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in allTexts)
            {
                if (text != itemUI.nameText)
                {
                    itemUI.amountText = text;
                    break;
                }
            }
        }

        // 레시피 정보 설정
        if (itemUI.nameText != null)
            itemUI.nameText.text = recipe.outputItem.itemName;

        // 아이콘 설정
        if (itemUI.iconImage != null && recipe.outputItem.itemIcon != null)
            itemUI.iconImage.sprite = recipe.outputItem.itemIcon;

        // 초기 수량 표시
        int currentAmount = _recipeCraftAmounts.ContainsKey(recipe) ? _recipeCraftAmounts[recipe] : 1;
        if (itemUI.amountText != null)
            itemUI.amountText.text = currentAmount.ToString();

        // 버튼 이벤트 연결
        if (itemUI.minusButton != null)
        {
            itemUI.minusButton.onClick.AddListener(() => OnMinusClicked(recipe, itemUI));
        }

        if (itemUI.plusButton != null)
        {
            itemUI.plusButton.onClick.AddListener(() => OnPlusClicked(recipe, itemUI));
        }

        if (itemUI.makeOrderButton != null)
        {
            itemUI.makeOrderButton.onClick.AddListener(() => OnMakeProductionOrderClicked(recipe));

            // 초기 버튼 상태 설정
            UpdateMakeOrderButton(recipe, itemUI);
        }

        // RecipeItemUI 저장
        _recipeItemUIs[recipe] = itemUI;
    }


    /// <summary>
    /// 수량 감소 버튼 클릭
    /// </summary>
    private void OnMinusClicked(CraftingRecipe recipe, RecipeItemUI itemUI)
    {
        if (!_recipeCraftAmounts.ContainsKey(recipe)) return;

        int currentAmount = _recipeCraftAmounts[recipe];
        if (currentAmount > 1)
        {
            currentAmount--;
            _recipeCraftAmounts[recipe] = currentAmount;

            // UI 업데이트
            if (itemUI.amountText != null)
                itemUI.amountText.text = currentAmount.ToString();

            UpdateMakeOrderButton(recipe, itemUI);
        }
    }

    /// <summary>
    /// 수량 증가 버튼 클릭
    /// </summary>
    private void OnPlusClicked(CraftingRecipe recipe, RecipeItemUI itemUI)
    {
        if (!_recipeCraftAmounts.ContainsKey(recipe)) return;

        int currentAmount = _recipeCraftAmounts[recipe];
        if (currentAmount < 99)
        {
            currentAmount++;
            _recipeCraftAmounts[recipe] = currentAmount;

            // UI 업데이트
            if (itemUI.amountText != null)
                itemUI.amountText.text = currentAmount.ToString();

            UpdateMakeOrderButton(recipe, itemUI);
        }
    }

    /// <summary>
    /// 생산 주문 생성 버튼 클릭
    /// </summary>
    private void OnMakeProductionOrderClicked(CraftingRecipe recipe)
    {
        if (recipe == null)
        {
            Debug.LogWarning("[ProductionUI] 레시피가 null입니다.");
            return;
        }

        int amount = _recipeCraftAmounts.ContainsKey(recipe) ? _recipeCraftAmounts[recipe] : 1;

        // 재료 체크
        if (!CheckMaterials(recipe, amount))
        {
            Debug.LogWarning($"[ProductionUI] 재료가 부족합니다!");
            ShowInsufficientMaterialsMessage(recipe, amount);
            return;
        }

        // Order 생성 콜백 호출 (worker는 null)
        _onStartProductionCallback?.Invoke(recipe, amount, null);

        // UI 닫기
        UIManager.instance.HidePanel(UIPanelType.ProductionUI);

        Debug.Log($"[ProductionUI] {recipe.outputItem.itemName} x{amount} Order가 생성되었습니다. 제재소를 다시 클릭하여 직원을 할당하세요.");
    }

    /// <summary>
    /// 재료가 충분한지 확인합니다
    /// </summary>
    private bool CheckMaterials(CraftingRecipe recipe, int amount)
    {
        if (recipe == null || recipe.requiredMaterials == null) return false;

        foreach (var cost in recipe.requiredMaterials)
        {
            int required = cost.amount * amount;
            int current = InventoryManager.instance.GetItemCount(cost.item);

            if (current < required)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 제작 버튼 상태 업데이트
    /// </summary>
    private void UpdateMakeOrderButton(CraftingRecipe recipe, RecipeItemUI itemUI)
    {
        if (itemUI.makeOrderButton == null) return;

        int amount = _recipeCraftAmounts.ContainsKey(recipe) ? _recipeCraftAmounts[recipe] : 1;
        bool canCraft = CheckMaterials(recipe, amount);

        itemUI.makeOrderButton.interactable = canCraft;

        // 버튼 텍스트 변경 (선택사항)
        var buttonText = itemUI.makeOrderButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = canCraft ? "제작 시작" : "재료 부족";
        }
    }

    /// <summary>
    /// 재료 부족 메시지 표시
    /// </summary>
    private void ShowInsufficientMaterialsMessage(CraftingRecipe recipe, int amount)
    {
        string message = $"재료 부족:\n";
        foreach (var cost in recipe.requiredMaterials)
        {
            int required = cost.amount * amount;
            int current = InventoryManager.instance.GetItemCount(cost.item);

            if (current < required)
            {
                message += $"- {cost.item.itemName}: {current}/{required}\n";
            }
        }

        Debug.Log(message);
        // TODO: UI 팝업 메시지로 표시
    }


    /// <summary>
    /// 닫기 버튼 클릭
    /// </summary>
    private void OnCloseClicked()
    {
        UIManager.instance.HidePanel(UIPanelType.ProductionUI);
    }

    /// <summary>
    /// UI 강제 새로고침 (인벤토리 변경 시 등)
    /// </summary>
    public void RefreshUI()
    {
        // 모든 RecipeItem의 버튼 상태 업데이트
        foreach (var kvp in _recipeItemUIs)
        {
            var recipe = kvp.Key;
            var itemUI = kvp.Value;

            UpdateMakeOrderButton(recipe, itemUI);
        }
    }

    /// <summary>
    /// RecipeItem UI 요소를 담는 헬퍼 클래스
    /// </summary>
    private class RecipeItemUI
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI amountText;
        public Image iconImage;
        public Button minusButton;
        public Button plusButton;
        public Button makeOrderButton;
    }
}
