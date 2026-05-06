using UnityEngine;

/// <summary>
/// 전쟁의 안개(Fog of War) 시스템.
///
/// 아키텍처:
///   - bool[,] _revealed : 한 번 탐색된 타일은 영구적으로 공개 (탐험 기반)
///   - RevealAround()    : 직원이 타일에 도달할 때마다 원형 반경 내 안개 제거
///   - ISaveModule       : 5KB 비트마스크로 저장/복원 (SaveOrder = 15)
///
/// 연동:
///   - MapGenerator.Start()       → InitializeFog() (전체 안개 초기화)
///   - EmployeeMovement           → RevealAround() (타일 이동/착지 시)
///   - MapRenderer.fogTilemap     → ClearFogAt() / RestoreFog()
///
/// 인스펙터 연결:
///   - revealRadius : 직원 시야 반경 (타일 단위, 기본 8)
/// </summary>
public class FogOfWarManager : DestroySingleton<FogOfWarManager>, ISaveModule
{
    #region 설정

    [Header("안개 설정")]
    [SerializeField] private int revealRadius = 8;

    #endregion

    #region 내부 상태

    /// <summary>탐색된 타일 여부 (true = 영구 공개)</summary>
    private bool[,] _revealed;

    #endregion

    #region 프로퍼티

    /// <summary>탐색 그리드 (읽기 전용, 저장 복원용)</summary>
    public bool[,] Revealed => _revealed;

    #endregion

    #region 초기화

    protected override void Awake()
    {
        base.Awake();
        _revealed = new bool[GameMap.MAP_WIDTH, GameMap.MAP_HEIGHT];
    }

    #endregion

    #region 공개 API

    /// <summary>
    /// 안개를 전체 초기화합니다 (모든 타일 미탐색 → 안개로 가림).
    /// 새 게임 시작 시 MapGenerator.Start()에서 호출됩니다.
    /// </summary>
    public void InitializeFog()
    {
        _revealed = new bool[GameMap.MAP_WIDTH, GameMap.MAP_HEIGHT];
        MapGenerator.instance?.MapRendererInstance?.InitializeFog();
        Debug.Log("[FogOfWarManager] 안개 초기화 완료");
    }

    /// <summary>
    /// 지정 타일을 중심으로 원형 반경의 안개를 제거합니다.
    /// 이미 탐색된 타일은 건너뜁니다 (Tilemap 호출 최소화).
    /// </summary>
    /// <param name="center">중심 타일 좌표 (발 위치)</param>
    public void RevealAround(Vector2Int center)
    {
        if (_revealed == null) return;

        MapRenderer renderer = MapGenerator.instance?.MapRendererInstance;
        if (renderer == null) return;

        int r2 = revealRadius * revealRadius;

        for (int dx = -revealRadius; dx <= revealRadius; dx++)
        {
            for (int dy = -revealRadius; dy <= revealRadius; dy++)
            {
                if (dx * dx + dy * dy > r2) continue;

                int nx = center.x + dx;
                int ny = center.y + dy;

                if (nx < 0 || nx >= GameMap.MAP_WIDTH)  continue;
                if (ny < 0 || ny >= GameMap.MAP_HEIGHT) continue;
                if (_revealed[nx, ny]) continue; // 이미 탐색됨 → 스킵

                _revealed[nx, ny] = true;
                renderer.ClearFogAt(nx, ny);
            }
        }
    }

    /// <summary>
    /// 특정 타일이 탐색 완료된 상태인지 확인합니다.
    /// </summary>
    public bool IsRevealed(int x, int y)
    {
        if (_revealed == null) return false;
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return false;
        return _revealed[x, y];
    }

    #endregion

    #region ISaveModule

    /// <summary>DayCycle(5), MapGenerator(10) 이후 / ZoneManager(25) 이전</summary>
    public int SaveOrder => 15;

    /// <summary>
    /// bool[,] → byte[] 비트마스크로 인코딩하여 저장합니다.
    /// 인덱스 = y * MAP_WIDTH + x
    /// </summary>
    public void Capture(SaveData data)
    {
        if (_revealed == null) return;

        int total    = GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT;
        int byteCount = (total + 7) / 8;
        byte[] mask  = new byte[byteCount];

        for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
        {
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                if (!_revealed[x, y]) continue;
                int bit = y * GameMap.MAP_WIDTH + x;
                mask[bit >> 3] |= (byte)(1 << (bit & 7));
            }
        }

        data.fog = new FogSaveData { revealedBitmask = mask };
    }

    /// <summary>
    /// byte[] 비트마스크 → bool[,] 로 디코딩한 뒤 안개 타일맵을 갱신합니다.
    /// </summary>
    public void Restore(SaveData data)
    {
        if (data.fog?.revealedBitmask == null)
        {
            // 이전 세이브 파일 (fog 없음) → 모든 타일 공개
            _revealed = new bool[GameMap.MAP_WIDTH, GameMap.MAP_HEIGHT];
            for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
                for (int x = 0; x < GameMap.MAP_WIDTH; x++)
                    _revealed[x, y] = true;

            MapGenerator.instance?.MapRendererInstance?.RestoreFog(_revealed);
            return;
        }

        byte[] mask = data.fog.revealedBitmask;
        _revealed   = new bool[GameMap.MAP_WIDTH, GameMap.MAP_HEIGHT];

        for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
        {
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                int bit = y * GameMap.MAP_WIDTH + x;
                if (bit / 8 < mask.Length && (mask[bit >> 3] & (1 << (bit & 7))) != 0)
                    _revealed[x, y] = true;
            }
        }

        MapGenerator.instance?.MapRendererInstance?.RestoreFog(_revealed);
        Debug.Log("[FogOfWarManager] 안개 상태 복원 완료");
    }

    public void PostRestore(SaveData data) { }

    #endregion
}
