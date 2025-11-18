using UnityEngine;

public class ChoppableTree : MonoBehaviour, IHarvestable, IWorkTarget
{
    [Header("나무 설정")]
    [SerializeField] private float growthTime = 60f; // 성장 시간
    [SerializeField] private float chopTime = 5f; // 벌목 시간
    
    [Header("수확물")]
    [SerializeField] private ItemData woodItem;
    [SerializeField] private int woodYield = 5;
    [SerializeField] private ItemData seedItem;
    [SerializeField] private int seedYield = 2;
    
    [Header("성장 단계 스프라이트")]
    [SerializeField] private Sprite seedlingSprite;
    [SerializeField] private Sprite youngSprite;
    [SerializeField] private Sprite matureSprite;
    
    private SpriteRenderer spriteRenderer;
    private float currentGrowth = 0f;
    private bool isFullyGrown = false;
    private bool isBeingChopped = false;
    
    private enum TreeStage
    {
        Seedling,   // 묘목
        Young,      // 어린 나무
        Mature      // 다 자란 나무
    }
    
    private TreeStage currentStage = TreeStage.Seedling;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }
    
    void Start()
    {
        UpdateVisual();
        
        // 테스트용: 50% 확률로 이미 다 자란 나무로 시작
        if (Random.Range(0f, 1f) < 0.5f)
        {
            currentGrowth = growthTime;
            isFullyGrown = true;
            currentStage = TreeStage.Mature;
            UpdateVisual();
        }
    }
    
    void Update()
    {
        if (!isFullyGrown)
        {
            Grow(Time.deltaTime);
        }
    }
    
    private void Grow(float deltaTime)
    {
        currentGrowth += deltaTime;
        
        // 성장 단계 업데이트
        float growthPercent = currentGrowth / growthTime;
        
        TreeStage newStage = TreeStage.Seedling;
        if (growthPercent >= 1f)
        {
            newStage = TreeStage.Mature;
            isFullyGrown = true;
        }
        else if (growthPercent >= 0.5f)
        {
            newStage = TreeStage.Young;
        }
        
        if (newStage != currentStage)
        {
            currentStage = newStage;
            UpdateVisual();
        }
    }
    
    private void UpdateVisual()
    {
        switch (currentStage)
        {
            case TreeStage.Seedling:
                if (seedlingSprite != null) spriteRenderer.sprite = seedlingSprite;
                transform.localScale = Vector3.one * 0.5f;
                break;
                
            case TreeStage.Young:
                if (youngSprite != null) spriteRenderer.sprite = youngSprite;
                transform.localScale = Vector3.one * 0.75f;
                break;
                
            case TreeStage.Mature:
                if (matureSprite != null) spriteRenderer.sprite = matureSprite;
                transform.localScale = Vector3.one;
                break;
        }
    }
    
    #region IHarvestable 구현
    
    public bool CanHarvest()
    {
        return isFullyGrown && !isBeingChopped;
    }
    
    public void Harvest()
    {
        if (!CanHarvest()) return;
        
        Debug.Log($"[Tree] 나무 벌목됨!");
        
        // 자원 드롭
        if (InventoryManager.instance != null)
        {
            if (woodItem != null)
            {
                InventoryManager.instance.AddItem(woodItem, woodYield);
            }
            if (seedItem != null && Random.Range(0f, 1f) < 0.5f) // 50% 확률로 씨앗
            {
                InventoryManager.instance.AddItem(seedItem, seedYield);
            }
        }
        
        // 그루터기로 변경하거나 제거
        CreateStump();
        Destroy(gameObject);
    }
    
    public float GetHarvestTime()
    {
        return chopTime;
    }
    
    public WorkType GetHarvestType()
    {
        return WorkType.Chopping;
    }
    
    #endregion
    
    #region IWorkTarget 구현
    
    public Vector3 GetWorkPosition()
    {
        return transform.position + new Vector3(0, -0.5f, 0); // 나무 앞 위치
    }
    
    public WorkType GetWorkType()
    {
        return WorkType.Chopping;
    }
    
    public float GetWorkTime()
    {
        return chopTime;
    }
    
    public bool IsWorkAvailable()
    {
        return CanHarvest();
    }
    
    public void CompleteWork(Employee worker)
    {
        Harvest();
    }
    
    public void CancelWork(Employee worker)
    {
        isBeingChopped = false;
    }
    
    #endregion
    
    private void CreateStump()
    {
        // 나무 그루터기 생성 (선택사항)
        // GameObject stump = Instantiate(stumpPrefab, transform.position, Quaternion.identity);
    }
    
    void OnMouseEnter()
    {
        if (CanHarvest())
        {
            spriteRenderer.color = new Color(1.2f, 1.2f, 1f);
        }
    }
    
    void OnMouseExit()
    {
        spriteRenderer.color = Color.white;
    }
    
    // 디버그용
    void OnDrawGizmos()
    {
        if (isFullyGrown)
        {
            Gizmos.color = Color.green;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }
        
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}