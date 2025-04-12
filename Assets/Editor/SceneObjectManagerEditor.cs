using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(SceneObjectManager))]
public class SceneObjectManagerEditor : Editor
{
    private string savePath = "Assets/Resources/SceneObjectsData.json";
    private bool showAdvancedOptions = false;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    
    public override void OnInspectorGUI()
    {
        InitStyles();
        
        SceneObjectManager sceneObjectManager = (SceneObjectManager)target;
        
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space(15);
        
        // 标题区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("场景物体存档工具", headerStyle);
        EditorGUILayout.Space(5);
        
        // 路径选择区域
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("存档路径:");
        EditorGUILayout.BeginVertical();
        savePath = EditorGUILayout.TextField(savePath);
        
        if (GUILayout.Button("浏览...", GUILayout.Width(80)))
        {
            string path = EditorUtility.SaveFilePanel("选择场景物体存档路径", 
                                                    Application.dataPath, 
                                                    "SceneObjectsData.json", 
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
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 操作按钮区域
        EditorGUILayout.BeginHorizontal();
        // 保存按钮
        if (GUILayout.Button(new GUIContent("保存数据", "将场景中所有物体数据保存到指定路径"), buttonStyle))
        {
            string fullPath = Path.Combine(Application.dataPath, "..", savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            
            string originalPath = sceneObjectManager.saveFilePath;
            sceneObjectManager.saveFilePath = fullPath;
            sceneObjectManager.SaveAllSceneObjects();
            sceneObjectManager.saveFilePath = originalPath;
            
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("保存成功", $"场景物体已保存到: {fullPath}", "确定");
        }
        
        // 加载按钮
        if (GUILayout.Button(new GUIContent("加载数据", "从指定路径加载场景物体数据"), buttonStyle))
        {
            string fullPath = Path.Combine(Application.dataPath, "..", savePath);
            if (File.Exists(fullPath))
            {
                string originalPath = sceneObjectManager.saveFilePath;
                sceneObjectManager.saveFilePath = fullPath;
                sceneObjectManager.LoadAllSceneObjects();
                sceneObjectManager.saveFilePath = originalPath;
                
                EditorUtility.DisplayDialog("加载成功", $"已从 {fullPath} 加载场景物体", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "找不到存档文件: " + fullPath, "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        
        // 场景物体统计区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"当前场景物体数量: {sceneObjectManager.sceneObjects.Count}", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // 高级选项
        showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "高级选项", true);
        if (showAdvancedOptions)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button("清除所有场景物体", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("确认清除", 
                    "确定要清除所有场景物体吗？此操作不可撤销！", 
                    "确定", "取消"))
                {
                    sceneObjectManager.ClearAllSceneObjects();
                }
            }
            GUI.backgroundColor = Color.white;
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }
    
    private void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.alignment = TextAnchor.MiddleCenter;
        }
        
        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.fixedHeight = 30;
        }
    }
}