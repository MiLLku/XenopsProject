using System.Collections.Generic;
using System.IO;
using UnityEngine;


public static class StampIOService
{
    // 스탬프 JSON 파일이 저장될 기본 경로
    public static string StampSavePath => $"{Application.dataPath}/GameData/Stamps";

    /// <summary>
    /// 스탬프 데이터를 JSON 파일로 저장합니다.
    /// </summary>
    public static void SaveStamp(StampData data)
    {
        if (data == null || string.IsNullOrEmpty(data.key)) return;

        Directory.CreateDirectory(StampSavePath); // 폴더가 없으면 생성
        string filePath = Path.Combine(StampSavePath, $"{data.key}.json");
        string json = JsonUtility.ToJson(data, true); // JsonUtility로 직렬화
        File.WriteAllText(filePath, json);
        Debug.Log($"[StampIOService] 스탬프 저장: {filePath}");
    }

    /// <summary>
    /// StampSavePath 폴더의 모든 .json 파일을 불러와 리스트로 반환합니다.
    /// </summary>
    public static List<StampData> LoadAllStamps()
    {
        List<StampData> stamps = new List<StampData>();
        if (!Directory.Exists(StampSavePath))
        {
            Debug.LogWarning($"[StampIOService] 스탬프 폴더를 찾을 수 없습니다: {StampSavePath}");
            return stamps;
        }

        string[] files = Directory.GetFiles(StampSavePath, "*.json");
        foreach (string filePath in files)
        {
            string json = File.ReadAllText(filePath);
            StampData data = JsonUtility.FromJson<StampData>(json);
            if (data != null)
            {
                stamps.Add(data);
            }
        }
        Debug.Log($"[StampIOService] 스탬프 {stamps.Count}개 로드 완료.");
        return stamps;
    }
}