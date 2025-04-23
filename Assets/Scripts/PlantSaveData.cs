using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 植物存档数据结构
[System.Serializable]
public class PlantSaveData
{
    // 添加类型标识字段
    public string plantType = "Plant";  // 新增字段
    // 基本植物状态
    public int plantID;
    public string plantName;
    public int currentStage;
    public int maxStages;
    public bool isWithered;
    public bool isImmortal;
    public string prefabPath; // 新增预制体路径字段
    
    // 位置和旋转信息
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
    public SerializableVector3 scale;
    
    // 生长阶段信息
    public List<SerializablePlantStage> growthStages = new List<SerializablePlantStage>();
    
    // 构造函数
    public PlantSaveData() { }
    
    // 从植物实例创建存档数据
    public PlantSaveData(Plant plant)
    {
        plantID = plant.plantID;
        plantName = plant.plantName;
        currentStage = plant.currentStage;
        maxStages = plant.maxStages;
        isWithered = plant.IsWithered;
        isImmortal = plant.IsImmortal;
        
        // 如果当前阶段有效，则从当前阶段获取预制体路径
        if (plant.currentStage > 0 && plant.currentStage <= plant.growthStages.Count)
        {
            prefabPath = plant.growthStages[plant.currentStage - 1].prefabPath;
        }
        
        // 保存位置和旋转
        position = new SerializableVector3(plant.transform.position);
        rotation = new SerializableQuaternion(plant.transform.rotation);
        scale = new SerializableVector3(plant.transform.localScale);
        
        // 保存生长阶段
        foreach (var stage in plant.growthStages)
        {
            growthStages.Add(new SerializablePlantStage(stage));
        }
    }
    
    // 将存档数据转换回Plant.PlantStage列表
    public List<Plant.PlantStage> ConvertToPlantStages()
    {
        List<Plant.PlantStage> result = new List<Plant.PlantStage>();
        foreach (var serializableStage in growthStages)
        {
            result.Add(serializableStage.ToPlantStage());
        }
        return result;
    }
}

// 用于序列化Vector3
[System.Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;
    
    public SerializableVector3(Vector3 vector)
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

// 用于序列化Quaternion
[System.Serializable]
public struct SerializableQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;
    
    public SerializableQuaternion(Quaternion quaternion)
    {
        x = quaternion.x;
        y = quaternion.y;
        z = quaternion.z;
        w = quaternion.w;
    }
    
    public Quaternion ToQuaternion()
    {
        return new Quaternion(x, y, z, w);
    }
}

// 用于序列化Plant.PlantStage
[System.Serializable]
public class SerializablePlantStage
{
    public StageType stageType;
    public int plantID;
    public string plantName;
    public float growthRate;
    public float witherRate; // 新增枯萎速度参数
    public string prefabPath; // 新增预制体路径字段
    public List<int> prerequisitePlantIDs = new List<int>();
    public List<float> prerequisiteWeights = new List<float>();
    public List<int> updatePlantIDs = new List<int>();
    public List<float> updateWeights = new List<float>();
    public List<SerializableLightingData> associatedLights = new List<SerializableLightingData>();
    
    // 构造函数
    public SerializablePlantStage() { }
    
    // 从PlantStage创建SerializablePlantStage
    public SerializablePlantStage(Plant.PlantStage stage)
    {
        stageType = stage.stageType;
        plantID = stage.plantID;
        plantName = stage.plantName;
        growthRate = stage.growthRate;
        witherRate = stage.witherRate; // 保存枯萎速度
        prefabPath = stage.prefabPath; // 保存预制体路径
        
        // 复制列表
        if (stage.prerequisitePlantIDs != null)
            prerequisitePlantIDs = new List<int>(stage.prerequisitePlantIDs);
            
        if (stage.prerequisiteWeights != null)
            prerequisiteWeights = new List<float>(stage.prerequisiteWeights);
            
        if (stage.updatePlantIDs != null)
            updatePlantIDs = new List<int>(stage.updatePlantIDs);
            
        if (stage.updateWeights != null)
            updateWeights = new List<float>(stage.updateWeights);
        
        // 转换LightingData
        if (stage.associatedLights != null)
        {
            foreach (var light in stage.associatedLights)
            {
                associatedLights.Add(new SerializableLightingData(light));
            }
        }
    }
    
    // 转换回PlantStage
    public Plant.PlantStage ToPlantStage()
    {
        Plant.PlantStage stage = new Plant.PlantStage();
        
        stage.stageType = stageType;
        stage.plantID = plantID;
        stage.plantName = plantName;
        stage.growthRate = growthRate;
        stage.witherRate = witherRate; // 设置枯萎速度
        stage.prefabPath = prefabPath; // 设置预制体路径
        
        // 复制列表
        stage.prerequisitePlantIDs = new List<int>(prerequisitePlantIDs);
        stage.prerequisiteWeights = new List<float>(prerequisiteWeights);
        stage.updatePlantIDs = new List<int>(updatePlantIDs);
        stage.updateWeights = new List<float>(updateWeights);
        
        // 转换回LightingData
        stage.associatedLights = new List<LightingData>();
        foreach (var serializableLight in associatedLights)
        {
            stage.associatedLights.Add(serializableLight.ToLightingData());
        }
        
        return stage;
    }
}

// 用于序列化LightingData
[System.Serializable]
public class SerializableLightingData
{
    public float size;
    public bool isObstacle;
    public bool isSeed;
    public float lightHeight;
    // 注意：Texture2D不能直接序列化，我们可以存储路径或者编码后的字符串
    public string heightMapPath; // 使用资源路径
    public string edgeHeightMapPath; // 新增边缘高度图路径
    public float rotation;
    
    // 构造函数
    public SerializableLightingData() { }
    
    // 从LightingData创建SerializableLightingData
    public SerializableLightingData(LightingData data)
    {
        size = data.size;
        isObstacle = data.isObstacle;
        isSeed = data.isSeed;
        lightHeight = data.lightHeight;
        rotation = data.rotation;
        
        // 处理高度图
        if (data.heightMap != null)
        {
            heightMapPath = data.heightMap.name;
        }
        
        // 处理边缘高度图
        if (data.edgeHeightMap != null)
        {
            edgeHeightMapPath = data.edgeHeightMap.name;
        }
    }
    
    // 转换回LightingData
    public LightingData ToLightingData()
    {
        // 初始化为null，下面再加载
        Texture2D heightMap = null;
        Texture2D edgeHeightMap = null;
        
        // 尝试加载高度图
        if (!string.IsNullOrEmpty(heightMapPath))
        {
            heightMap = Resources.Load<Texture2D>("HeightMaps/" + heightMapPath);
        }
        
        // 尝试加载边缘高度图
        if (!string.IsNullOrEmpty(edgeHeightMapPath))
        {
            edgeHeightMap = Resources.Load<Texture2D>("EdgeHeightMaps/" + edgeHeightMapPath);
        }
        
        LightingData data = new LightingData(
            size: size,
            isObstacle: isObstacle,
            isSeed: isSeed,
            lightHeight: lightHeight,
            heightMap: heightMap,
            edgeHeightMap: edgeHeightMap, // 设置边缘高度图
            rotation: rotation
        );
        
        return data;
    }
}

// 用于序列化Vector2
[System.Serializable]
public struct SerializableVector2
{
    public float x;
    public float y;
    
    public SerializableVector2(Vector2 vector)
    {
        x = vector.x;
        y = vector.y;
    }
    
    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
    
    public SerializableVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}
