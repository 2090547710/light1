using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(PlantManager))]
public class PlantManagerEditor : Editor
{
    private string savePath = "Assets/Resources/PlantsData.json";
    
    public override void OnInspectorGUI()
    {
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        PlantManager plantManager = (PlantManager)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("植物存档工具", EditorStyles.boldLabel);
        
        // 保存路径
        savePath = EditorGUILayout.TextField("存档路径:", savePath);
        
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
            }
        }
        
        // 显示当前植物信息
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"当前活跃植物: {plantManager.activePlants.Count}", EditorStyles.boldLabel);
    }
} 