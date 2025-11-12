// --- 파일 13: CameraController.cs (줌 기능 추가 버전) ---
// 메인 카메라에 붙여서 WASD 이동, 줌, 맵 경계 제한을 담당합니다.

using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("카메라의 이동 속도")]
    [SerializeField] private float moveSpeed = 20f;

    // ★★★ [추가된 부분 1: 줌 변수] ★★★
    [Header("줌 설정")]
    [Tooltip("마우스 휠 줌 속도")]
    [SerializeField] private float zoomSpeed = 10f;
    
    [Tooltip("최대 확대 (가장 가까이). 값이 작을수록 가까워집니다.")]
    [SerializeField] private float minOrthographicSize = 3f;
    
    [Tooltip("최대 축소 (가장 멀리). 값이 클수록 멀어집니다.")]
    [SerializeField] private float maxOrthographicSize = 25f;
    // ★★★ [추가된 부분 1 끝] ★★★

    private Camera _cam;

    // 맵 경계 변수 (LateUpdate에서 매번 계산됨)
    private float _minX, _maxX, _minY, _maxY;
    
    void Start()
    {
        _cam = Camera.main;
        
        // 시작 시 카메라 위치를 스폰 지점 근처로 설정 (예: 100, 150)
        // (MapGenerator의 baseGroundLevel 값을 참조하는 것이 좋습니다)
        // (일단 100, 150으로 하드코딩)
        Vector3 startPos = new Vector3(100f, 150f, transform.position.z);
        
        // 시작 위치가 경계를 벗어나지 않도록 즉시 한 번 계산 및 적용
        CalculateBounds();
        ClampPosition(ref startPos);
        transform.position = startPos;
    }

    void Update()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal"); 
        float verticalInput = Input.GetAxisRaw("Vertical");     
        Vector3 moveDirection = (Vector3.right * horizontalInput) + (Vector3.up * verticalInput);
        transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime);

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0f) 
        {
            float currentZoom = _cam.orthographicSize;
            currentZoom -= scrollInput * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minOrthographicSize, maxOrthographicSize);
            _cam.orthographicSize = currentZoom;
        }
    }

    void LateUpdate()
    {
        CalculateBounds();
        
        Vector3 pos = transform.position;
        
        ClampPosition(ref pos);
        
        transform.position = pos;
    }
    
    private void CalculateBounds()
    {
        float camHeight = _cam.orthographicSize;
        float camWidth = _cam.orthographicSize * _cam.aspect;

        _minX = 0f + camWidth;
        _maxX = (float)GameMap.MAP_WIDTH - camWidth;
        _minY = 0f + camHeight;
        _maxY = (float)GameMap.MAP_HEIGHT - camHeight;
    }

    private void ClampPosition(ref Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);
    }
    
    public Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -_cam.transform.position.z; 
        return _cam.ScreenToWorldPoint(mousePos);
    }
}