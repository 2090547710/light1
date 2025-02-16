#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class LightingDebugWindow : EditorWindow
{
    private Lighting targetLighting;
    
    [MenuItem("Window/Lighting Debug")]
    public static void ShowWindow()
    {
        GetWindow<LightingDebugWindow>("Lighting Debug");
    }

    void OnEnable()
    {
        ObjectSelector.SelectionChanged += OnSelectedObjectChanged;
    }

    void OnDisable()
    {
        ObjectSelector.SelectionChanged -= OnSelectedObjectChanged;
    }

    void OnSelectedObjectChanged(GameObject obj)
    {
        targetLighting = obj?.GetComponent<Lighting>();
        Repaint();
    }

    void OnGUI()
    {
        if (targetLighting == null)
        {
            GUILayout.Label("No Lighting component selected");
            return;
        }

        EditorGUI.BeginChangeCheck();

        // 区域形状参数
        targetLighting.areaShape = (AreaShape)EditorGUILayout.EnumPopup("Shape", targetLighting.areaShape);
        targetLighting.areaType = (AreaType)EditorGUILayout.EnumPopup("Type", targetLighting.areaType);
        
        // 尺寸参数
        targetLighting.areaSizeX = EditorGUILayout.Slider("Size X", targetLighting.areaSizeX, 0, 100);
        if (targetLighting.areaShape == AreaShape.Rectangle)
        {
            targetLighting.areaSizeZ = EditorGUILayout.Slider("Size Z", targetLighting.areaSizeZ, 0, 100);
        }
        
        // 高度参数
        targetLighting.areaHeight = EditorGUILayout.Slider("Height", targetLighting.areaHeight, -1, 1);
        targetLighting.lightHeight = EditorGUILayout.Slider("Light Height", targetLighting.lightHeight, 0, 1);

        // 显示影响节点数
        EditorGUILayout.LabelField($"Light Nodes: {targetLighting.LightNodesAffected}");
        EditorGUILayout.LabelField($"Dark Nodes: {targetLighting.DarkNodesAffected}");

        if (EditorGUI.EndChangeCheck())
        {
            LightingManager.UpdateLighting();
        }
    }
}
#endif 