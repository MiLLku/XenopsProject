using UnityEngine;

/// <summary>
/// 게임 내 모든 ID를 중앙 관리하는 레지스트리
/// ScriptableObject 생성 시 참고용
/// </summary>
public static class GameIDRegistry
{
    // ===== ID 범위 정의 =====

    /// <summary>
    /// 타일 ID 범위 (1000~1999)
    /// </summary>
    public static class Tiles
    {
        public const int AIR = 1000;
        public const int DIRT = 1001;
        public const int STONE = 1002;
        public const int IRON_ORE = 1003;
        public const int COAL = 1004;
        public const int WOOD_TILE = 1005;

        // 범위 체크
        public const int MIN = 1000;
        public const int MAX = 1999;

        public static bool IsValid(int id) => id >= MIN && id <= MAX;
    }

    /// <summary>
    /// 아이템 ID 범위 (2000~2999)
    /// </summary>
    public static class Items
    {
        // 기본 자원 (2000~2099)
        public const int WOOD = 2000;
        public const int STONE = 2001;
        public const int IRON_ORE = 2002;
        public const int COAL = 2003;
        public const int WATER = 2004;

        // 가공 자원 (2100~2199)
        public const int WOODEN_PLANK = 2100;
        public const int WOODEN_BEAM = 2101;
        public const int IRON_INGOT = 2102;
        public const int STEEL_BAR = 2103;
        public const int LEATHER = 2104;

        // 도구 (2200~2299)
        public const int WOODEN_PICKAXE = 2200;
        public const int STONE_PICKAXE = 2201;
        public const int IRON_PICKAXE = 2202;
        public const int AXE = 2203;
        public const int HAMMER = 2204;

        // 음식 (2300~2399)
        public const int BREAD = 2300;
        public const int COOKED_MEAT = 2301;
        public const int SOUP = 2302;

        // 포션 (2400~2499)
        public const int HEALTH_POTION = 2400;
        public const int MANA_POTION = 2401;
        public const int STAMINA_POTION = 2402;

        // 범위 체크
        public const int MIN = 2000;
        public const int MAX = 2999;

        public static bool IsValid(int id) => id >= MIN && id <= MAX;
    }

    /// <summary>
    /// 건물 ID 범위 (3000~3999)
    /// </summary>
    public static class Buildings
    {
        // 생산 건물 (3000~3099)
        public const int SAWMILL = 3000;
        public const int FORGE = 3001;
        public const int SMELTER = 3002;
        public const int ALCHEMY_TABLE = 3003;
        public const int WINDMILL = 3004;
        public const int LOOM = 3005;
        public const int TANNERY = 3006;

        // 저장 건물 (3100~3199)
        public const int WOODEN_CHEST = 3100;
        public const int IRON_CHEST = 3101;
        public const int WAREHOUSE = 3102;

        // 주거 건물 (3200~3299)
        public const int WOODEN_HOUSE = 3200;
        public const int STONE_HOUSE = 3201;
        public const int BARRACKS = 3202;

        // 농업 건물 (3300~3399)
        public const int FARM = 3300;
        public const int BARN = 3301;
        public const int GREENHOUSE = 3302;

        // 범위 체크
        public const int MIN = 3000;
        public const int MAX = 3999;

        public static bool IsValid(int id) => id >= MIN && id <= MAX;
    }

    /// <summary>
    /// 레시피 ID 범위 (5000~5999)
    /// </summary>
    public static class Recipes
    {
        // 목재 가공 (5000~5099)
        public const int WOODEN_PLANK = 5000;
        public const int WOODEN_BEAM = 5001;
        public const int WOODEN_DOOR = 5002;

        // 금속 가공 (5100~5199)
        public const int IRON_INGOT = 5100;
        public const int STEEL_BAR = 5101;
        public const int IRON_PICKAXE = 5102;

        // 음식 조리 (5300~5399)
        public const int BREAD = 5300;
        public const int COOKED_MEAT = 5301;
        public const int SOUP = 5302;

        // 연금술 (5400~5499)
        public const int HEALTH_POTION = 5400;
        public const int MANA_POTION = 5401;
        public const int STAMINA_POTION = 5402;

        // 범위 체크
        public const int MIN = 5000;
        public const int MAX = 5999;

        public static bool IsValid(int id) => id >= MIN && id <= MAX;
    }

    /// <summary>
    /// 직원/유닛 ID 범위 (4000~4999)
    /// </summary>
    public static class Employees
    {
        public const int WORKER = 4000;
        public const int BUILDER = 4001;
        public const int MINER = 4002;
        public const int FARMER = 4003;

        // 범위 체크
        public const int MIN = 4000;
        public const int MAX = 4999;

        public static bool IsValid(int id) => id >= MIN && id <= MAX;
    }

    // ===== 유틸리티 메서드 =====

    /// <summary>
    /// ID가 어떤 타입에 속하는지 확인
    /// </summary>
    public static string GetIDType(int id)
    {
        if (Tiles.IsValid(id)) return "Tile";
        if (Items.IsValid(id)) return "Item";
        if (Buildings.IsValid(id)) return "Building";
        if (Employees.IsValid(id)) return "Employee";
        if (Recipes.IsValid(id)) return "Recipe";

        return "Unknown";
    }

    /// <summary>
    /// ID 범위가 겹치는지 검사
    /// </summary>
    public static bool ValidateID(int id, out string errorMessage)
    {
        string type = GetIDType(id);

        if (type == "Unknown")
        {
            errorMessage = $"ID {id}는 유효한 범위에 속하지 않습니다!";
            return false;
        }

        errorMessage = "";
        return true;
    }

    /// <summary>
    /// 다음 사용 가능한 ID 추천 (디버그용)
    /// </summary>
    public static void LogNextAvailableIDs()
    {
        Debug.Log("=== 다음 사용 가능한 ID ===");
        Debug.Log($"타일 (Tile): {Tiles.MIN} ~ {Tiles.MAX}");
        Debug.Log($"아이템 (Item): {Items.MIN} ~ {Items.MAX}");
        Debug.Log($"건물 (Building): {Buildings.MIN} ~ {Buildings.MAX}");
        Debug.Log($"직원 (Employee): {Employees.MIN} ~ {Employees.MAX}");
        Debug.Log($"레시피 (Recipe): {Recipes.MIN} ~ {Recipes.MAX}");
    }
}
