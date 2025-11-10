// --- 파일 13: CameraController.cs (줌 기능 추가 버전) ---
// 메인 카메라에 붙여서 WASD 이동, 줌, 맵 경계 제한을 담당합니다.

using UnityEngine;
using StampSystem; // GameMap의 상수에 접근하기 위해 필요

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
        // --- 1. 이동 (WASD) ---
        float horizontalInput = Input.GetAxisRaw("Horizontal"); 
        float verticalInput = Input.GetAxisRaw("Vertical");     
        Vector3 moveDirection = (Vector3.right * horizontalInput) + (Vector3.up * verticalInput);
        transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime);

        // --- 2. 줌 (마우스 휠) ---
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        // ★★★ [디버그 로그 추가] ★★★
        // 휠을 스크롤할 때만 콘솔에 로그가 찍히는지 확인합니다.
        if (scrollInput != 0f) 
        {
            Debug.Log("휠 입력 감지! 값: " + scrollInput); 
            // ★★★ [디버그 로그 추가 끝] ★★★

            float currentZoom = _cam.orthographicSize;
            currentZoom -= scrollInput * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minOrthographicSize, maxOrthographicSize);
            _cam.orthographicSize = currentZoom;
        }
    }

    // Update()가 끝난 '직후'에 호출되어 좌표를 최종 보정합니다.
    // ★★★ [수정된 부분 3: LateUpdate() 로직 수정] ★★★
    void LateUpdate()
    {
        // 1. 줌이 변경되었을 수 있으므로, '매 프레임' 경계를 다시 계산합니다.
        CalculateBounds();
        
        // 2. 현재 카메라 위치 가져오기
        Vector3 pos = transform.position;
        
        // 3. 위치를 경계 사이로 제한
        ClampPosition(ref pos);
        
        // 4. 보정된 위치로 다시 설정
        transform.position = pos;
    }

    /// <summary>
    /// 현재 카메라 줌 크기에 맞춰 맵 경계를 다시 계산합니다.
    /// </summary>
    private void CalculateBounds()
    {
        float camHeight = _cam.orthographicSize;
        float camWidth = _cam.orthographicSize * _cam.aspect;

        // 타일은 (0, 0)부터 시작합니다.
        _minX = 0f + camWidth;
        _maxX = (float)GameMap.MAP_WIDTH - camWidth;
        _minY = 0f + camHeight;
        _maxY = (float)GameMap.MAP_HEIGHT - camHeight;
    }

    /// <summary>
    /// Vector3 위치를 계산된 경계(min/max) 사이로 제한합니다.
    /// </summary>
    private void ClampPosition(ref Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);
    }
}