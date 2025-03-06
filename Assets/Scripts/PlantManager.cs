using UnityEngine;
using System.IO;

public class PlantManager : MonoBehaviour
{
    public Plant plantPrefab;
    
    // CSV文件路径（编辑器中）
    public string csvFilePath = "Assets/Resources/PlantData/plant_stages.csv";
    
    // Resources中的CSV路径（运行时）
    public string resourcePath = "PlantData/plant_stages";
    
    void Start()
    {
        // 在运行时从Resources加载
        Plant newPlant = Instantiate(plantPrefab, Vector3.zero, Quaternion.identity);
        newPlant.LoadGrowthStagesFromResources(resourcePath);
    }
    
    // 编辑器方法：从CSV创建植物
    public Plant CreatePlantFromCSV()
    {
        Plant newPlant = Instantiate(plantPrefab, Vector3.zero, Quaternion.identity);
        
        // 检查文件是否存在
        if (File.Exists(csvFilePath))
        {
            newPlant.LoadGrowthStagesFromCSV(csvFilePath);
            return newPlant;
        }
        else
        {
            Debug.LogError($"CSV文件不存在: {csvFilePath}");
            return null;
        }
    }
    
    // 编辑器方法：保存植物数据到CSV
    public void SavePlantToCSV(Plant plant)
    {
        if (plant != null)
        {
            plant.SaveGrowthStagesToCSV(csvFilePath);
        }
    }
} 