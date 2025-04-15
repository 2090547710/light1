using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 场景物体类型枚举
public enum SceneObjectType
{
    Map,        // 地图
    Obstacle,   // 障碍物
    Water,      // 水
    BeginPoint, // 开始点
    EndPoint    // 结束点
}

// 场景物体属性类，用于挂载到场景物体上
[System.Serializable]
public class SceneObjectProperty : MonoBehaviour
{
    // 物体类型
    public SceneObjectType objectType = SceneObjectType.Map;
    
    // 预制体路径
    public string prefabPath;
    
    // 光源数据列表
    public List<LightingData> lightSourcesData = new List<LightingData>();
    
    // 光源组件列表
    public List<Lighting> lightSources = new List<Lighting>();
    
    // 添加光源数据
    public void AddLightSourceData(LightingData lightData)
    {
        lightSourcesData.Add(lightData);
        // 如果物体已经添加了Lighting组件，则初始化该组件
        Lighting lighting = GetComponent<Lighting>();
        if (lighting == null)
        {
            lighting = gameObject.AddComponent<Lighting>();
        }
        lighting.InitializeFromData(lightData);
        lightSources.Add(lighting);
    }
    
    // 移除光源数据
    public void RemoveLightSourceData(int index)
    {
        if (index >= 0 && index < lightSourcesData.Count)
        {
            // 如果对应的光源组件存在，移除它
            if (index < lightSources.Count)
            {
                Lighting lighting = lightSources[index];
                if (lighting != null)
                {
                    lighting.RemoveLighting();
                    Destroy(lighting);
                }
                lightSources.RemoveAt(index);
            }
            lightSourcesData.RemoveAt(index);
        }
    }
    
    // 清除所有光源数据
    public void ClearLightSourcesData()
    {
        // 移除所有光源组件
        foreach (var lighting in lightSources)
        {
            if (lighting != null)
            {
                lighting.RemoveLighting();
                Destroy(lighting);
            }
        }
        lightSources.Clear();
        lightSourcesData.Clear();
    }
    
    // 应用所有光源数据
    public void ApplyLightSources()
    {
        if (lightSourcesData.Count == 0)
        {
            return;
        }
        
        // 先清除现有光源组件
        foreach (var lighting in lightSources)
        {
            if (lighting != null)
            {
                lighting.RemoveLighting();
                Destroy(lighting);
            }
        }
        lightSources.Clear();
        
        // 根据数据创建并初始化光源组件
        foreach (var data in lightSourcesData)
        {
            var newLight = gameObject.AddComponent<Lighting>();
            newLight.InitializeFromData(data);
            lightSources.Add(newLight);
        }
        
        // 更新光照管理器
        LightingManager.tree.Insert(gameObject);
        LightingManager.UpdateDirtyLights();
    }
    
    // 获取活跃光源的光照数据
    public List<LightingData> GetLightingDataFromLightSources()
    {
        List<LightingData> lightingDataList = new List<LightingData>();
        
        foreach (Lighting light in lightSources)
        {
            // 创建新的LightingData
            LightingData data = new LightingData(
                size: light.size,
                isObstacle: light.isObstacle,
                isSeed: light.isSeed,
                lightHeight: light.lightHeight,
                heightMap: light.heightMap,
                rotation: light.rotation
            );
            
            lightingDataList.Add(data);
        }
        
        return lightingDataList;
    }
    
    // Start方法中应用光源
    private void Start()
    {
        if (SceneObjectManager.Instance != null)
        {
            SceneObjectManager.Instance.RegisterSceneObject(this);
        }
        ApplyLightSources();
    }
    
    // 当组件启用时注册到管理器
    private void OnEnable()
    {
        if (SceneObjectManager.Instance != null)
        {
            SceneObjectManager.Instance.RegisterSceneObject(this);
        }
    }
    
    // 当组件禁用时从管理器注销
    private void OnDisable()
    {
        if (SceneObjectManager.Instance != null)
        {
            SceneObjectManager.Instance.UnregisterSceneObject(this);
        }
    }
}

// 场景物体保存数据结构
[System.Serializable]
public class SceneObjectSaveData
{
    // 添加类型标识字段
    public string objectType = "SceneObject";
    
    // 物体类型
    public SceneObjectType sceneObjectType;
    
    // 预制体路径
    public string prefabPath;
    
    // 位置、旋转和缩放
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
    public SerializableVector3 scale;
    
    // 光源数据列表
    public List<SerializableLightingData> lightSourcesData = new List<SerializableLightingData>();
    
    // 构造函数
    public SceneObjectSaveData() { }
    
    // 从场景物体创建存档数据
    public SceneObjectSaveData(SceneObjectProperty sceneObject)
    {
        sceneObjectType = sceneObject.objectType;
        prefabPath = sceneObject.prefabPath;
        
        // 保存位置、旋转和缩放
        position = new SerializableVector3(sceneObject.transform.position);
        rotation = new SerializableQuaternion(sceneObject.transform.rotation);
        scale = new SerializableVector3(sceneObject.transform.localScale);
        
        // 保存光源数据（改为从活跃光源加载）
        List<LightingData> activeLightingData = sceneObject.GetLightingDataFromLightSources();
        foreach (var light in activeLightingData)
        {
            lightSourcesData.Add(new SerializableLightingData(light));
        }
    }
    
    // 应用到场景物体
    public void ApplyToSceneObject(SceneObjectProperty sceneObject)
    {
        sceneObject.objectType = sceneObjectType;
        sceneObject.prefabPath = prefabPath;
        
        // 应用变换
        sceneObject.transform.position = position.ToVector3();
        sceneObject.transform.rotation = rotation.ToQuaternion();
        sceneObject.transform.localScale = scale.ToVector3();
        
        // 清除现有光源
        sceneObject.ClearLightSourcesData();
        
        // 应用光源数据
        foreach (var serializableLight in lightSourcesData)
        {
            sceneObject.AddLightSourceData(serializableLight.ToLightingData());
        }
    }
}