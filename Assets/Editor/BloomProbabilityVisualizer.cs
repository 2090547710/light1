using UnityEngine;
using UnityEditor;

public class BloomProbabilityVisualizer : EditorWindow
{
    private float bloomSteepness = 10f;
    private float bloomThreshold = 0.5f;
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/Bloom Probability Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<BloomProbabilityVisualizer>("Bloom曲线可视化");
    }
    
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Bloom概率曲线参数", EditorStyles.boldLabel);
        bloomSteepness = EditorGUILayout.Slider("曲线陡度", bloomSteepness, 1f, 20f);
        bloomThreshold = EditorGUILayout.Slider("阈值", bloomThreshold, 0f, 1f);
        
        Rect rect = GUILayoutUtility.GetRect(200, 200);
        DrawGraph(rect);
        
        EditorGUILayout.EndScrollView();
    }
    
    void DrawGraph(Rect rect)
    {
        Handles.BeginGUI();
        
        // 绘制背景和边框
        Handles.color = new Color(0.2f, 0.2f, 0.2f, 1);
        Handles.DrawSolidRectangleWithOutline(rect, new Color(0.2f, 0.2f, 0.2f, 1), Color.white);
        
        // 绘制坐标轴
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height), new Vector3(rect.x + rect.width, rect.y + rect.height));
        Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.y + rect.height));
        
        // 绘制曲线
        Handles.color = Color.green;
        Vector3 prevPoint = Vector3.zero;
        
        for (float x = 0; x <= 1; x += 0.01f)
        {
            float brightnessRatio = x;
            float bloomProbability = 1f / (1f + Mathf.Exp(-bloomSteepness * (brightnessRatio - bloomThreshold)));
            
            Vector3 point = new Vector3(
                rect.x + x * rect.width,
                rect.y + rect.height - bloomProbability * rect.height,
                0
            );
            
            if (x > 0)
            {
                Handles.DrawLine(prevPoint, point);
            }
            
            prevPoint = point;
        }
        
        // 绘制阈值线
        Handles.color = Color.yellow;
        Vector3 thresholdPoint = new Vector3(
            rect.x + bloomThreshold * rect.width,
            rect.y + rect.height,
            0
        );
        Vector3 thresholdPointTop = new Vector3(
            rect.x + bloomThreshold * rect.width,
            rect.y,
            0
        );
        Handles.DrawDottedLine(thresholdPoint, thresholdPointTop, 2);
        
        Handles.EndGUI();
        
        // 添加标签
        GUI.Label(new Rect(rect.x, rect.y + rect.height + 5, 100, 20), "0");
        GUI.Label(new Rect(rect.x + rect.width - 20, rect.y + rect.height + 5, 100, 20), "1");
        GUI.Label(new Rect(rect.x - 25, rect.y, 100, 20), "1");
        GUI.Label(new Rect(rect.x - 25, rect.y + rect.height - 10, 100, 20), "0");
        GUI.Label(new Rect(rect.x + bloomThreshold * rect.width - 10, rect.y + rect.height + 5, 100, 20), "阈值");
    }
} 