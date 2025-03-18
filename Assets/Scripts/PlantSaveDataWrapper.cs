using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ... 现有代码 ...

// 添加在PlantManager类中，或者作为独立文件

// 用于JSON序列化植物存档数据列表的包装类
[System.Serializable]
public class PlantSaveDataWrapper
{
    public List<PlantSaveData> plants = new List<PlantSaveData>();
    
    // 构造函数
    public PlantSaveDataWrapper()
    {
        plants = new List<PlantSaveData>();
    }
    
    // 基于现有列表的构造函数
    public PlantSaveDataWrapper(List<PlantSaveData> plantsList)
    {
        plants = new List<PlantSaveData>(plantsList);
    }
    
    // 添加单个植物数据
    public void AddPlant(PlantSaveData plantData)
    {
        plants.Add(plantData);
    }
    
    // 添加多个植物数据
    public void AddPlants(IEnumerable<PlantSaveData> plantsData)
    {
        plants.AddRange(plantsData);
    }
    
    // 清空植物数据
    public void Clear()
    {
        plants.Clear();
    }
    
    // 获取植物数量
    public int Count => plants.Count;
    
    // 转换为JSON字符串
    public string ToJson(bool prettyPrint = true)
    {
        return JsonUtility.ToJson(this, prettyPrint);
    }
    
    // 从JSON字符串创建
    public static PlantSaveDataWrapper FromJson(string json)
    {
        return JsonUtility.FromJson<PlantSaveDataWrapper>(json);
    }
}

// ... 现有代码 ...