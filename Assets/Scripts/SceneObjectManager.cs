using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SceneObjectManager : MonoBehaviour
{
    // 单例模式
    public static SceneObjectManager Instance { get; private set; }
    
    // 场景中所有的物体列表
    public List<SceneObjectProperty> sceneObjects = new List<SceneObjectProperty>();
    
    // 默认物体预制体（作为备用）
    public GameObject sceneObjectPrefab;
    
    // 存档路径
    public string saveFilePath = "SaveData/SceneObjects.json";
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // 预先分配足够大的数组，参考PlantManager中的做法
        List<Vector4> initialArray = new List<Vector4>(new Vector4[64]);
        Shader.SetGlobalVectorArray("_EndPointPositions", initialArray);
    }
    
    void Update()
    {
        // 将起点和终点坐标传递给shader
        if (GameManager.Instance != null)
        {
            bool hasBeginPoint = GameManager.Instance.beginPoint != null;
            
            // 起点坐标
            if (hasBeginPoint)
            {
                Vector3 beginPos = GameManager.Instance.beginPoint.transform.position;
                Shader.SetGlobalVector("_BeginPointPos", beginPos);
                Shader.SetGlobalFloat("_ShowBeginPoint", 1.0f);
            }
            else
            {
                Shader.SetGlobalFloat("_ShowBeginPoint", 0.0f);
            }
            
            // 终点坐标 - 修改为支持多个终点
            if (GameManager.Instance.endPoints.Count > 0)
            {
                // 最多支持64个终点
                int endPointCount = Mathf.Min(GameManager.Instance.endPoints.Count, 64);
                
                // 创建Vector4数组而不是Vector3数组
                Vector4[] endPositions = new Vector4[endPointCount];
                
                for (int i = 0; i < endPointCount; i++)
                {
                    if (GameManager.Instance.endPoints[i] != null)
                    {
                        // 将Vector3转换为Vector4，w分量设为0
                        Vector3 pos = GameManager.Instance.endPoints[i].transform.position;
                        endPositions[i] = new Vector4(pos.x, pos.y, pos.z, 0);
                    }
                }
                
                // 设置终点位置数组
                Shader.SetGlobalVectorArray("_EndPointPositions", endPositions);
                Shader.SetGlobalInt("_EndPointCount", endPointCount);
                Shader.SetGlobalFloat("_ShowEndPoint", 1.0f);
            }
            else
            {
                Shader.SetGlobalInt("_EndPointCount", 0);
                Shader.SetGlobalFloat("_ShowEndPoint", 0.0f);
            }
        }
    }
    
    // 注册物体
    public void RegisterSceneObject(SceneObjectProperty sceneObject)
    {
        if (!sceneObjects.Contains(sceneObject))
        {
            sceneObjects.Add(sceneObject);
        }
    }
    
    // 移除注册
    public void UnregisterSceneObject(SceneObjectProperty sceneObject)
    {
        if (sceneObjects.Contains(sceneObject))
        {
            sceneObjects.Remove(sceneObject);
        }
    }
    
    // 保存所有场景物体
    public void SaveAllSceneObjects(string customPath = null)
    {
        List<SceneObjectSaveData> saveDataList = new List<SceneObjectSaveData>();
        
        foreach (var obj in sceneObjects)
        {
            SceneObjectSaveData saveData = new SceneObjectSaveData(obj);
            saveDataList.Add(saveData);
        }
        
        // 将保存数据列表包装到一个类中
        SceneObjectSaveDataWrapper wrapper = new SceneObjectSaveDataWrapper
        {
            sceneObjects = saveDataList
        };
        
        // 序列化为JSON
        string json = JsonUtility.ToJson(wrapper, true);
        
        // 使用自定义路径或默认路径
        string pathToSave = customPath ?? saveFilePath;
        
        // 创建保存目录（如果不存在）
        string directory = Path.GetDirectoryName(pathToSave);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 写入文件
        File.WriteAllText(pathToSave, json);
        Debug.Log($"场景物体已保存到: {pathToSave}");
    }
    
    // 从存档加载场景物体
    public void LoadAllSceneObjects(string customPath = null)
    {
        string pathToLoad = customPath ?? saveFilePath;
        
        if (!File.Exists(pathToLoad))
        {
            Debug.LogWarning($"找不到保存文件: {pathToLoad}");
            return;
        }
        
        // 读取JSON
        string json = File.ReadAllText(pathToLoad);
        
        // 反序列化
        SceneObjectSaveDataWrapper wrapper = JsonUtility.FromJson<SceneObjectSaveDataWrapper>(json);
        
        // 清除现有场景物体
        ClearAllSceneObjects();
        
        // 创建新的场景物体
        foreach (var saveData in wrapper.sceneObjects)
        {
            GameObject newObject;
            
            // 尝试从预制体路径加载
            if (!string.IsNullOrEmpty(saveData.prefabPath))
            {
                GameObject prefab = Resources.Load<GameObject>(saveData.prefabPath);
                if (prefab != null)
                {
                    newObject = Instantiate(prefab, saveData.position.ToVector3(), saveData.rotation.ToQuaternion());
                }
                else
                {
                    Debug.LogWarning($"无法加载预制体: {saveData.prefabPath}，使用默认预制体代替");
                    newObject = Instantiate(sceneObjectPrefab, saveData.position.ToVector3(), saveData.rotation.ToQuaternion());
                }
            }
            else
            {
                // 如果没有预制体路径，使用默认预制体
                newObject = Instantiate(sceneObjectPrefab, saveData.position.ToVector3(), saveData.rotation.ToQuaternion());
            }
            
            newObject.transform.localScale = saveData.scale.ToVector3();
            
            // 检查并移除已有的SceneObjectProperty组件
            SceneObjectProperty existingProperty = newObject.GetComponent<SceneObjectProperty>();
            if (existingProperty != null)
            {
                Destroy(existingProperty);
            }
            
            // 添加属性组件
            SceneObjectProperty property = newObject.AddComponent<SceneObjectProperty>();
            
            // 应用保存数据
            saveData.ApplyToSceneObject(property);
            
            // 根据物体类型设置对应的layer
            switch (property.objectType)
            {
                case SceneObjectType.Map:
                    newObject.layer = 8;
                    break;
                case SceneObjectType.Obstacle:
                    newObject.layer = 7;
                    break;
                case SceneObjectType.Water:
                    newObject.layer = 9;
                    break;
                case SceneObjectType.BeginPoint:
                    newObject.layer = 11;
                    // 更新GameManager中的开始点引用
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.beginPoint = newObject;
                    }
                    break;
                case SceneObjectType.EndPoint:
                    newObject.layer = 10;
                    // 更新GameManager中的终点引用
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.endPoints.Add(newObject);
                    }
                    break;
                case SceneObjectType.Player:
                    newObject.layer = 11;
                    break;
            }
            
            // 注册到管理器
            RegisterSceneObject(property);
        }
        
        Debug.Log($"已加载 {wrapper.sceneObjects.Count} 个场景物体");
    }
    
    // 清除所有场景物体
    public void ClearAllSceneObjects()
    {
        // 创建一个新列表以避免在迭代时修改原列表
        List<SceneObjectProperty> objectsToDestroy = new List<SceneObjectProperty>(sceneObjects);
        
        foreach (var obj in objectsToDestroy)
        {
            if (obj != null)
            {
                // 清除所有光源
                foreach (var light in obj.lightSources.ToList())
                {
                    light.RemoveLighting();
                    obj.lightSources.Remove(light);
                    Destroy(light);
                }
                // 从四叉树中移除
                LightingManager.tree.Remove(obj.gameObject);
                Destroy(obj.gameObject);
            }
        }
        
        sceneObjects.Clear();
    }
}

// 场景物体保存数据包装类
[System.Serializable]
public class SceneObjectSaveDataWrapper
{
    public List<SceneObjectSaveData> sceneObjects = new List<SceneObjectSaveData>();
}