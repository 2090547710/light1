using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public static class CSVParser
{
    // 从CSV文件加载植物生长阶段数据
    public static List<Plant.PlantStage> LoadPlantStagesFromCSV(string filePath)
    {
        List<Plant.PlantStage> stages = new List<Plant.PlantStage>();
        
        if (!File.Exists(filePath))
        {
            Debug.LogError($"CSV文件不存在: {filePath}");
            return stages;
        }
        
        try
        {
            // 读取所有行
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length <= 1) // 检查是否只有标题行或空文件
            {
                Debug.LogWarning($"CSV文件为空或只有标题行: {filePath}");
                return stages;
            }
            
            // 跳过标题行，从第二行开始解析
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                
                Plant.PlantStage stage = ParsePlantStage(lines[i]);
                if (stage != null)
                {
                    stages.Add(stage);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析CSV文件时出错: {e.Message}");
        }
        
        return stages;
    }
    
    // 解析单行CSV数据为PlantStage对象
    private static Plant.PlantStage ParsePlantStage(string line)
    {
        string[] values = line.Split(',');
        
        // 检查数据完整性
        if (values.Length < 4) // 至少需要阶段类型、植物ID、植物名称和一个光照数据
        {
            Debug.LogWarning($"CSV行数据不完整: {line}");
            return null;
        }
        
        try
        {
            Plant.PlantStage stage = new Plant.PlantStage();
            
            // 解析基本信息
            stage.stageType = (StageType)System.Enum.Parse(typeof(StageType), values[0].Trim());
            stage.plantID = int.Parse(values[1].Trim());
            stage.plantName = values[2].Trim();
            
            // 解析光照数据
            stage.associatedLights = new List<LightingData>();
            
            // 从第4列开始，每7列为一组光照数据
            for (int i = 3; i < values.Length; i += 7)
            {
                if (i + 6 < values.Length) // 确保有完整的7列数据
                {
                    LightingData lightData = new LightingData(
                        size: float.Parse(values[i].Trim()),
                        isObstacle: bool.Parse(values[i + 1].Trim()),
                        isSeed: bool.Parse(values[i + 2].Trim()),
                        lightHeight: float.Parse(values[i + 3].Trim())
                        // 注意：heightMap需要单独处理，因为它是Texture2D类型
                        // tiling和offset也需要单独解析为Vector2
                    );
                    
                    // 解析tiling和offset (格式如 "x|y")
                    string[] tiling = values[i + 5].Trim().Split('|');
                    if (tiling.Length == 2)
                    {
                        lightData.tiling = new Vector2(
                            float.Parse(tiling[0].Trim()),
                            float.Parse(tiling[1].Trim())
                        );
                    }
                    
                    string[] offset = values[i + 6].Trim().Split('|');
                    if (offset.Length == 2)
                    {
                        lightData.offset = new Vector2(
                            float.Parse(offset[0].Trim()),
                            float.Parse(offset[1].Trim())
                        );
                    }
                    
                    // 处理heightMap (存储路径，需要加载)
                    string heightMapPath = values[i + 4].Trim();
                    if (!string.IsNullOrEmpty(heightMapPath))
                    {
                        // 从Resources文件夹加载纹理
                        lightData.heightMap = Resources.Load<Texture2D>(heightMapPath);
                    }
                    
                    stage.associatedLights.Add(lightData);
                }
            }
            
            return stage;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析植物阶段数据时出错: {line}\n{e.Message}");
            return null;
        }
    }
    
    // 将植物生长阶段数据保存为CSV文件
    public static void SavePlantStagesToCSV(List<Plant.PlantStage> stages, string filePath)
    {
        try
        {
            List<string> lines = new List<string>();
            
            // 添加标题行
            lines.Add("StageType,PlantID,PlantName,Size,IsObstacle,IsSeed,LightHeight,HeightMapPath,Tiling,Offset");
            
            // 添加数据行
            foreach (var stage in stages)
            {
                foreach (var light in stage.associatedLights)
                {
                    string heightMapPath = "";
                    if (light.heightMap != null)
                    {
                        // 获取资源路径（假设在Resources文件夹中）
                        heightMapPath = light.heightMap.name;
                    }
                    
                    string line = $"{stage.stageType},{stage.plantID},{stage.plantName}," +
                                 $"{light.size},{light.isObstacle},{light.isSeed},{light.lightHeight}," +
                                 $"{heightMapPath},{light.tiling.x}|{light.tiling.y},{light.offset.x}|{light.offset.y}";
                    
                    lines.Add(line);
                }
            }
            
            // 写入文件
            File.WriteAllLines(filePath, lines.ToArray());
            Debug.Log($"成功保存植物生长阶段数据到: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存CSV文件时出错: {e.Message}");
        }
    }
} 