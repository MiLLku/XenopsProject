using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 특정 모드에서 화면을 어둡게 만드는 오버레이 관리
/// </summary>
public class ScreenOverlay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image overlayImage;
    
    [Header("Overlay Settings")]
    [SerializeField] private Color normalOverlayColor = new Color(0, 0, 0, 0); // 투명
    [SerializeField] private Color miningOverlayColor = new Color(0, 0, 0, 0.3f); // 30% 어두움
    [SerializeField] private Color harvestOverlayColor = new Color(0, 0.1f, 0, 0.2f); // 약간 녹색
    [SerializeField] private Color demolishOverlayColor = new Color(0.1f, 0, 0, 0.2f); // 약간 빨강
    
    [SerializeField] private float fadeSpeed = 5f; // 전환 속도
    
    private Color targetColor;
    private InteractionManager interactionManager;
    
    void Start()
    {
        interactionManager = InteractionManager.instance;
        
        if (interactionManager == null)
        {
            Debug.LogError("[ScreenOverlay] InteractionManager를 찾을 수 없습니다!");
            gameObject.SetActive(false);
            return;
        }
        
        if (overlayImage == null)
        {
            overlayImage = GetComponent<Image>();
        }
        
        // 모드 변경 이벤트 구독
        interactionManager.OnModeChanged += OnModeChanged;
        
        // 초기 색상 설정
        OnModeChanged(interactionManager.GetCurrentMode());
        overlayImage.color = targetColor;
    }
    
    void OnDestroy()
    {
        if (interactionManager != null)
        {
            interactionManager.OnModeChanged -= OnModeChanged;
        }
    }
    
    void Update()
    {
        // 부드럽게 색상 전환
        if (overlayImage.color != targetColor)
        {
            overlayImage.color = Color.Lerp(overlayImage.color, targetColor, fadeSpeed * Time.deltaTime);
        }
    }
    
    private void OnModeChanged(InteractionManager.InteractMode mode)
    {
        switch (mode)
        {
            case InteractionManager.InteractMode.Normal:
            case InteractionManager.InteractMode.Build:
                targetColor = normalOverlayColor;
                break;
                
            case InteractionManager.InteractMode.Mine:
                targetColor = miningOverlayColor;
                break;
                
            case InteractionManager.InteractMode.Harvest:
                targetColor = harvestOverlayColor;
                break;
                
            case InteractionManager.InteractMode.Demolish:
                targetColor = demolishOverlayColor;
                break;
        }
    }
}