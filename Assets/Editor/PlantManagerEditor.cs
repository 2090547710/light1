using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(PlantManager))]
public class PlantManagerEditor : Editor
{
    private string savePath = "Assets/Resources/PlantsData.json";
    private const string SavePathPrefsKey = "PlantManager_SavePath";
    
    private void OnEnable()
    {
        // 从EditorPrefs加载上次保存的路径
        savePath = EditorPrefs.GetString(SavePathPrefsKey, "Assets/Resources/PlantsData.json");
    }
    
    public override void OnInspectorGUI()
    {
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        PlantManager plantManager = (PlantManager)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("植物存档工具", EditorStyles.boldLabel);
        
        // 保存路径
        string newPath = EditorGUILayout.TextField("存档路径:", savePath);
        if (newPath != savePath)
        {
            savePath = newPath;
            // 当路径变更时，保存到EditorPrefs
            EditorPrefs.SetString(SavePathPrefsKey, savePath);
        }
        
        EditorGUILayout.BeginHorizontal();
        
        // 保存按钮
        if (GUILayout.Button("保存植物数据", GUILayout.Height(30)))
        {
            string fullPath = Path.Combine(Application.dataPath, "..", savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            plantManager.SaveAllPlants(fullPath);
            AssetDatabase.Refresh();
        }
        
        // 加载按钮
        GUI.enabled = Application.isPlaying; // 只在游戏运行时启用此按钮
        if (GUILayout.Button("加载植物数据", GUILayout.Height(30)))
        {
            string fullPath = Path.Combine(Application.dataPath, "..", savePath);
            if (File.Exists(fullPath))
            {
                plantManager.LoadAllPlants(fullPath);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "找不到存档文件: " + fullPath, "确定");
            }
        }
        GUI.enabled = true; // 恢复启用状态
        
        EditorGUILayout.EndHorizontal();
        
        // 浏览按钮
        if (GUILayout.Button("选择存档路径..."))
        {
            string path = EditorUtility.SaveFilePanel("选择植物存档路径", 
                                                    Application.dataPath, 
                                                    "PlantsData.json", 
                                                    "json");
            if (!string.IsNullOrEmpty(path))
            {
                // 转换为相对路径
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                savePath = path;
                // 保存选择的路径到EditorPrefs
                EditorPrefs.SetString(SavePathPrefsKey, savePath);
            }
        }
        
        // 数据库加载工具
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("数据库加载工具", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // 加载种子映射按钮
        if (GUILayout.Button("重新加载种子映射", GUILayout.Height(30)))
        {
            plantManager.LoadSeedMappings();
            plantManager.UpdateAllPlantCounts();
            plantManager.RebuildUpdatablePlants();
            EditorUtility.DisplayDialog("成功", "种子映射数据已重新加载", "确定");
        }
        
        // 加载植物数据库按钮
        if (GUILayout.Button("重新加载植物数据库", GUILayout.Height(30)))
        {
            plantManager.LoadPlantDatabase();
            plantManager.UpdateAllPlantCounts();
            plantManager.RebuildUpdatablePlants();
            EditorUtility.DisplayDialog("成功", "植物数据库已重新加载", "确定");
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 清除按钮 - 新增功能
        EditorGUILayout.Space(10);
        if (GUILayout.Button("清除所有植物", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认操作", "确定要清除所有植物吗？此操作无法撤销。", "确定", "取消"))
            {
                plantManager.ClearAllPlants();
            }
        }
        
        // 显示当前植物信息
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"当前活跃植物: {plantManager.activePlants.Count}", EditorStyles.boldLabel);
        
        // 显示植物列表
        if (plantManager.activePlants.Count > 0)
        {
            EditorGUILayout.LabelField("活跃植物列表:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < plantManager.activePlants.Count; i++)
            {
                Plant plant = plantManager.activePlants[i];
                if (plant != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i+1}. {plant.plantName} (ID: {plant.plantID})");
                    if (GUILayout.Button("选择", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = plant.gameObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;
        }
    }
} 