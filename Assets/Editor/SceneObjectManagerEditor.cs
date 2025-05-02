// Assets/Editor/SceneObjectManagerEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(SceneObjectManager))]
public class SceneObjectManagerEditor : Editor
{
    // 在OnEnable中初始化savePath
    private void OnEnable()
    {
        // 这样可以确保Editor加载时获取最新的路径
        SceneObjectManager sceneManager = (SceneObjectManager)target;
        if (sceneManager != null)
        {
            // savePath不再需要作为字段存储
        }
    }
    
    public override void OnInspectorGUI()
    {
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        SceneObjectManager sceneManager = (SceneObjectManager)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("场景物体管理工具", EditorStyles.boldLabel);
        
        // 保存路径 - 直接使用sceneManager中的值
        string tempPath = EditorGUILayout.TextField("存档路径:", sceneManager.saveFilePath);
        if (tempPath != sceneManager.saveFilePath)
        {
            // 只有当路径变更时才更新
            sceneManager.saveFilePath = tempPath;
            EditorUtility.SetDirty(sceneManager); // 标记为已修改，确保保存
        }
        
        EditorGUILayout.BeginHorizontal();
        
        // 保存按钮
        if (GUILayout.Button("保存场景物体", GUILayout.Height(30)))
        {
            sceneManager.SaveAllSceneObjects();
            AssetDatabase.Refresh();
        }
        
        // 加载按钮
        GUI.enabled = Application.isPlaying; // 只在游戏运行时启用此按钮
        if (GUILayout.Button("加载场景物体", GUILayout.Height(30)))
        {
            sceneManager.LoadAllSceneObjects();
        }
        GUI.enabled = true; // 恢复启用状态
        
        EditorGUILayout.EndHorizontal();
        
        // 浏览按钮
        if (GUILayout.Button("选择存档路径..."))
        {
            string path = EditorUtility.SaveFilePanel("选择场景物体存档路径", 
                                                    Application.dataPath, 
                                                    "SceneObjects.json", 
                                                    "json");
            if (!string.IsNullOrEmpty(path))
            {
                // 转换为相对路径
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                sceneManager.saveFilePath = path;
                EditorUtility.SetDirty(sceneManager); // 标记为已修改，确保保存
            }
        }
        
        // 清除按钮
        EditorGUILayout.Space(10);
        if (GUILayout.Button("清除所有场景物体", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认操作", "确定要清除所有场景物体吗？此操作无法撤销。", "确定", "取消"))
            {
                sceneManager.ClearAllSceneObjects();
            }
        }
        
        // 显示当前物体信息
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"当前场景物体: {sceneManager.sceneObjects.Count}", EditorStyles.boldLabel);
        
        // 显示场景物体列表
        EditorGUI.indentLevel++;
        for (int i = 0; i < sceneManager.sceneObjects.Count; i++)
        {
            SceneObjectProperty obj = sceneManager.sceneObjects[i];
            if (obj != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i+1}. {obj.objectType} - {obj.name}");
                if (GUILayout.Button("选择", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = obj.gameObject;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUI.indentLevel--;
    }
}