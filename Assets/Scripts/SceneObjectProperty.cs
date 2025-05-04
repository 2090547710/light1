using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 场景物体类型枚举
public enum SceneObjectType
{
    Map,        // 地图
    Obstacle,   // 障碍物
    Water,      // 水
    BeginPoint, // 开始点
    EndPoint,   // 结束点
    Player      // 玩家
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
    
    // 交互相关变量
    public float interactionRadius = 3f;  // 交互半径
    public UnityEvent onInteract;         // 交互事件
    private bool playerInRange = false;   // 玩家是否在范围内
    
    // 检测玩家是否在交互范围内
    private void Update()
    {
        if (objectType == SceneObjectType.BeginPoint || objectType == SceneObjectType.EndPoint)
        {
            // 获取玩家位置
            Vector3 playerPosition = PlayerPathfinding.Instance.transform.position;
            // 创建只保留xz坐标的新位置
            Vector3 playerPositionXZ = new Vector3(playerPosition.x, 0, playerPosition.z);
            Vector3 objectPositionXZ = new Vector3(transform.position.x, 0, transform.position.z);
            // 计算xz平面距离
            float distance = Vector3.Distance(objectPositionXZ, playerPositionXZ);
            
            // 更新玩家是否在范围内
            playerInRange = distance <= interactionRadius;
            // 检测输入
            if (playerInRange && Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }
    }
    
    // 交互方法
    public void Interact()
    {

        // 触发交互事件
        onInteract?.Invoke();
            
        // 根据不同类型执行不同操作
        switch (objectType)
        {
            case SceneObjectType.BeginPoint:
                Debug.Log("与开始点交互");
                // 在这里添加开始点特定逻辑
                LevelSelectUI.Instance.LoadPreviousLevel();
                break;
                    
            case SceneObjectType.EndPoint:
                Debug.Log("与结束点交互");
                // 在这里添加结束点特定逻辑
                LevelSelectUI.Instance.CompleteAndLoadNextLevel();
                break;
        }
        
    }
    
 
    
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
                edgeHeightMap: light.edgeHeightMap,
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
        
        // 如果当前物体是玩家，则将其设为摄像机的跟踪目标
        if (objectType == SceneObjectType.Player)
        {
            CameraController cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                cameraController.target = this.transform;
            }
            
            // 从子对象获取Animator并启用
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
            }
        }
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