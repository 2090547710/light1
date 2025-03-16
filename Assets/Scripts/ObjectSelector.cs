using UnityEngine;
using System.Linq;
using UnityEditor;

public class ObjectSelector : MonoBehaviour
{
    public LayerMask selectableLayer;
    public float selectionRadius = 2f;
    public Material highlightMaterial;
    
    private GameObject selectedObject;
    private Material originalMaterial;

    // 添加公共属性访问选中对象
    public GameObject SelectedObject => selectedObject;

    // 添加序列化字段来存储临时调整值
    [System.Serializable]
    public class LightingProperties
    {
        public float size;
        public bool isObstacle;
        public bool isSeed;
        public Texture2D heightMap;
        public Vector2 tiling;
        public Vector2 offset;
        [Range(0, 1)] public float lightHeight;
    }

    [Header("光照属性编辑")]
    [SerializeField] private LightingProperties editingProperties = new LightingProperties();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            // 优先使用射线检测精确选择
            if (TryRaycastSelection()) return;
            
            // 使用四叉树区域查询
            TryQuadTreeSelection();
        }
    }

    bool TryRaycastSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, selectableLayer))
        {
            HandleSelection(hit.collider.gameObject);
            return true;
        }
        return false;
    }

    void TryQuadTreeSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // 使用四叉树查询附近对象
            Bounds queryArea = new Bounds(
                hit.point,
                new Vector3(selectionRadius, 1.0f, selectionRadius));
            
            var candidates = GameManager.Instance.tree.QueryArea(queryArea);
            
            // 精确距离检测
            GameObject closest = candidates
                .OrderBy(obj => Vector3.Distance(obj.transform.position, hit.point))
                .FirstOrDefault();

            if (closest != null) HandleSelection(closest);
        }
    }

    void HandleSelection(GameObject obj)
    {
        // 清除旧选择
        if (selectedObject != null)
        {
            selectedObject.GetComponent<Renderer>().material = originalMaterial;
        }

        // 设置新选择
        selectedObject = obj;
        var renderer = obj.GetComponent<Renderer>();
        originalMaterial = renderer.material;
        renderer.material = highlightMaterial;
        


        // 扩展选择事件
        SelectionChanged?.Invoke(obj);

        // 同步选中对象的Lighting属性到编辑器
        if (selectedObject.TryGetComponent<Lighting>(out var lighting))
        {
            editingProperties.size = lighting.size;
            editingProperties.isObstacle = lighting.isObstacle;
            editingProperties.isSeed = lighting.isSeed;
            editingProperties.heightMap = lighting.heightMap;
            editingProperties.tiling = lighting.tiling;
            editingProperties.offset = lighting.offset;
            editingProperties.lightHeight = lighting.lightHeight;
        }
        
        // 选择对象后自动打开编辑器窗口
        #if UNITY_EDITOR
        ObjectPropertiesWindow.ShowWindow(this);
        #endif
    }

    // 添加选择事件委托
    public delegate void SelectionHandler(GameObject selectedObj);
    public static event SelectionHandler SelectionChanged;

#if UNITY_EDITOR
    // 添加自定义编辑器扩展
    [CustomEditor(typeof(ObjectSelector))]
    public class ObjectSelectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            ObjectSelector selector = (ObjectSelector)target;
            if (selector.SelectedObject != null && 
                selector.SelectedObject.TryGetComponent<Lighting>(out var lighting))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("选中的光照属性", EditorStyles.boldLabel);
                
                // 创建可撤销的修改记录
                Undo.RecordObject(lighting, "Modify Lighting Properties");
                
                // 同步编辑属性到实际组件
                selector.editingProperties.size = EditorGUILayout.FloatField("Size", selector.editingProperties.size);
                // 确保size大于0
                if (selector.editingProperties.size <= 0)
                {
                    selector.editingProperties.size = 0.01f;
                    EditorGUILayout.HelpBox("Size必须大于0", MessageType.Warning);
                }
                
                selector.editingProperties.isSeed = EditorGUILayout.Toggle("Is Seed", selector.editingProperties.isSeed);
                if(selector.editingProperties.isSeed && selector.editingProperties.isObstacle)
                {
                    selector.editingProperties.isObstacle = false;
                    EditorGUILayout.HelpBox("Seed不能同时是Obstacle", MessageType.Warning);
                }
                selector.editingProperties.isObstacle = EditorGUILayout.Toggle("Is Obstacle", selector.editingProperties.isObstacle);
                if(selector.editingProperties.isObstacle && selector.editingProperties.isSeed)
                {
                    selector.editingProperties.isSeed = false;
                    EditorGUILayout.HelpBox("Obstacle不能同时是Seed", MessageType.Warning);
                }
                selector.editingProperties.heightMap = (Texture2D)EditorGUILayout.ObjectField("Height Map", selector.editingProperties.heightMap, typeof(Texture2D), false);
                selector.editingProperties.tiling = EditorGUILayout.Vector2Field("Tiling", selector.editingProperties.tiling);
                selector.editingProperties.offset = EditorGUILayout.Vector2Field("Offset", selector.editingProperties.offset);
                selector.editingProperties.lightHeight = EditorGUILayout.Slider("Light Height", selector.editingProperties.lightHeight, 0, 1);

                // 应用修改到实际组件
                lighting.size = Mathf.Max(0.01f, selector.editingProperties.size);
                lighting.isObstacle = selector.editingProperties.isObstacle;
                lighting.isSeed = selector.editingProperties.isSeed;
                lighting.heightMap = selector.editingProperties.heightMap;
                lighting.tiling = selector.editingProperties.tiling;
                lighting.offset = selector.editingProperties.offset;
                lighting.lightHeight = selector.editingProperties.lightHeight;

                // 新增亮度影响显示
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"亮度影响值: {lighting.TotalBrightnessImpact:F2}", 
                    new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold });

                // 立即应用修改
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(lighting);
                    lighting.OnValidate(); // 触发验证和更新
                }
            }
            
            // 添加打开属性编辑窗口的按钮
            EditorGUILayout.Space(10);
            if (GUILayout.Button("打开属性编辑窗口"))
            {
                ObjectPropertiesWindow.ShowWindow((ObjectSelector)target);
            }
        }
    }
    
    // 添加编辑器窗口类
    public class ObjectPropertiesWindow : EditorWindow
    {
        private ObjectSelector targetSelector;
        private Vector2 scrollPosition = Vector2.zero;
        private static ObjectPropertiesWindow window;
        
        // 添加菜单项
        [MenuItem("Tools/对象属性编辑器")]
        public static void ShowWindow()
        {
            window = GetWindow<ObjectPropertiesWindow>("对象属性编辑器");
            window.minSize = new Vector2(300, 500);
            window.Show();
        }
        
        // 重载方法，接受ObjectSelector参数
        public static void ShowWindow(ObjectSelector selector)
        {
            ShowWindow();
            window.targetSelector = selector;
        }
        
        private void OnGUI()
        {
            if (targetSelector == null || targetSelector.SelectedObject == null)
            {
                EditorGUILayout.HelpBox("未选中有效的对象", MessageType.Info);
                return;
            }
            
            GameObject selectedObject = targetSelector.SelectedObject;
            
            EditorGUILayout.LabelField($"选中对象: {selectedObject.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            
            // 处理植物组件
            if (selectedObject.TryGetComponent<Plant>(out var plant))
            {
                EditorGUILayout.LabelField("植物属性编辑", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                // 创建可撤销的修改记录
                Undo.RecordObject(plant, "修改植物属性");
                
                // 显示植物ID和名称
                EditorGUILayout.LabelField($"植物ID: {plant.plantID}");
                EditorGUILayout.LabelField($"植物名称: {plant.plantName}");
                
                // 显示当前生长阶段
                EditorGUILayout.LabelField($"当前阶段: {plant.currentStage}/{plant.maxStages}");
                
                // 显示是否枯萎
                EditorGUILayout.LabelField($"是否枯萎: {(plant.IsWithered ? "是" : "否")}");
                
                // 添加hasTriedBloom和hasTriedFruit的显示
                EditorGUILayout.LabelField($"已尝试开花: {(plant.HasTriedBloom ? "是" : "否")}");
                EditorGUILayout.LabelField($"已尝试结果: {(plant.HasTriedFruit ? "是" : "否")}");
                
                // 显示亮度比例
                EditorGUILayout.LabelField($"亮度比例: {plant.BrightnessRatio:P2}");
                
                // 显示开花概率
                EditorGUILayout.LabelField($"开花概率: {plant.BloomProbability:P2}");
                
                // 添加生长速度设置
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("生长速度设置", EditorStyles.boldLabel);
                
                // 记录GUI变更前的值
                float oldGrowthRate = plant.growthRate;
                float oldGrowthRateInfluence = plant.growthRateInfluence;
                
                // 生长速度编辑
                float newGrowthRate = EditorGUILayout.FloatField("生长速度:", plant.growthRate);
                if (newGrowthRate != oldGrowthRate)
                {
                    plant.growthRate = Mathf.Max(0.01f, newGrowthRate);

                    if (Application.isPlaying)
                    {
                        plant.CalculateBrightnessRatio();
                    }
                }
                
                // 生长速度影响系数编辑
                float newGrowthRateInfluence = EditorGUILayout.Slider("速度影响系数:", plant.growthRateInfluence, 0f, 1f);
                if (newGrowthRateInfluence != oldGrowthRateInfluence)
                {
                    plant.growthRateInfluence = newGrowthRateInfluence;
                    if (Application.isPlaying)
                    {
                        plant.CalculateBrightnessRatio();
                    }
                }
                
                // 添加开花设置
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("开花设置", EditorStyles.boldLabel);
                
                // 记录GUI变更前的值
                float oldBloomThreshold = plant.bloomThreshold;
                float oldBloomSteepness = plant.bloomSteepness;
                
                // 开花阈值编辑
                float newBloomThreshold = EditorGUILayout.Slider("开花阈值:", plant.bloomThreshold, 0f, 1f);
                if (newBloomThreshold != oldBloomThreshold)
                {
                    plant.bloomThreshold = newBloomThreshold;
                    if (Application.isPlaying)
                    {
                        plant.CalculateBrightnessRatio();
                    }
                }
                
                // Sigmoid陡峭度编辑
                float newBloomSteepness = EditorGUILayout.FloatField("Sigmoid陡峭度:", plant.bloomSteepness);
                if (newBloomSteepness != oldBloomSteepness)
                {
                    plant.bloomSteepness = Mathf.Max(0.1f, newBloomSteepness);
                    if (Application.isPlaying)
                    {
                        plant.CalculateBrightnessRatio();
                    }
                }
                
                EditorGUILayout.Space(10);
                
                // 添加概率曲线展示
                EditorGUILayout.LabelField("开花概率曲线", EditorStyles.boldLabel);
                
                // 创建曲线区域
                Rect curveRect = EditorGUILayout.GetControlRect(false, 200);
                DrawProbabilityCurve(curveRect, plant);
                
                // 显示当前亮度比例的标记
                if (Application.isPlaying)
                {
                    DrawCurrentBrightnessMarker(curveRect, plant);
                }
                
                EditorGUILayout.Space(10);
                
                // 添加按钮
                EditorGUILayout.BeginHorizontal();
                
                // 生长按钮
                if (GUILayout.Button("生长", GUILayout.Height(30)))
                {
                    plant.Grow();
                }
                
                // 开花按钮
                if (GUILayout.Button("开花", GUILayout.Height(30)))
                {
                    plant.TryBloom();
                }
                
                // 结果按钮
                if (GUILayout.Button("结果", GUILayout.Height(30)))
                {
                    plant.TryFruit();
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                // 添加计算概率按钮
                if (GUILayout.Button("计算概率", GUILayout.Height(30)))
                {
                    plant.CalculateBrightnessRatio();
                    Repaint(); // 刷新窗口显示
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // 添加枯萎按钮
                if (GUILayout.Button("枯萎", GUILayout.Height(30)))
                {
                    plant.Wither();
                }
                
                // 立即应用修改
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(plant);
                }
                
                EditorGUILayout.Space(10);
            }
            
            // 新增：光照组件列表编辑
            if (selectedObject.TryGetComponent<Plant>(out var plantComponent) && plantComponent.lightSources != null && plantComponent.lightSources.Count > 0) {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("光照组件列表编辑", EditorStyles.boldLabel);
                for (int i = 0; i < plantComponent.lightSources.Count; i++) {
                    var lightElement = plantComponent.lightSources[i];
                    EditorGUILayout.LabelField($"光照组件 {i}", EditorStyles.boldLabel);
                    Undo.RecordObject(lightElement, "Modify Lighting Properties");

                    float newSize = EditorGUILayout.FloatField("大小:", lightElement.size);
                    if (newSize != lightElement.size) {
                        lightElement.size = Mathf.Max(0.01f, newSize);
                    }

                    bool newIsSeed = EditorGUILayout.Toggle("是否为种子:", lightElement.isSeed);
                    if (newIsSeed != lightElement.isSeed) {
                        lightElement.isSeed = newIsSeed;
                        if (newIsSeed && lightElement.isObstacle) {
                            lightElement.isObstacle = false;
                            EditorGUILayout.HelpBox("种子不能同时为障碍物", MessageType.Warning);
                        }
                    }

                    bool newIsObstacle = EditorGUILayout.Toggle("是否为障碍物:", lightElement.isObstacle);
                    if (newIsObstacle != lightElement.isObstacle) {
                        lightElement.isObstacle = newIsObstacle;
                        if (newIsObstacle && lightElement.isSeed) {
                            lightElement.isSeed = false;
                            EditorGUILayout.HelpBox("障碍物不能同时为种子", MessageType.Warning);
                        }
                    }

                    float newLightHeight = EditorGUILayout.Slider("光照高度:", lightElement.lightHeight, 0, 1);
                    if (newLightHeight != lightElement.lightHeight) {
                        lightElement.lightHeight = newLightHeight;
                    }

                    Texture2D newHeightMap = (Texture2D)EditorGUILayout.ObjectField("高度图:", lightElement.heightMap, typeof(Texture2D), false);
                    if (newHeightMap != lightElement.heightMap) {
                        lightElement.heightMap = newHeightMap;
                    }

                    Vector2 newTiling = EditorGUILayout.Vector2Field("平铺:", lightElement.tiling);
                    if (newTiling != lightElement.tiling) {
                        lightElement.tiling = newTiling;
                    }

                    Vector2 newOffset = EditorGUILayout.Vector2Field("偏移:", lightElement.offset);
                    if (newOffset != lightElement.offset) {
                        lightElement.offset = newOffset;
                    }

                    if (GUI.changed)
                    {
                        EditorUtility.SetDirty(lightElement);
                        lightElement.OnValidate(); // 触发验证和更新
            
                    }
                    
                    EditorGUILayout.Space();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void OnSelectionChange()
        {
            // 当Unity编辑器中的选择改变时更新窗口
            if (Selection.activeGameObject != null)
            {
                var selector = FindObjectOfType<ObjectSelector>();
                if (selector != null)
                {
                    targetSelector = selector;
                    Repaint();
                }
            }
        }

        // 添加绘制概率曲线的方法
        private void DrawProbabilityCurve(Rect rect, Plant plant)
        {
            // 绘制背景和边框
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));
            
            // 计算调整后的阈值
            float adjustedThreshold = plant.bloomThreshold - (plant.growthRate * plant.growthRateInfluence);
            
            // 在曲线上绘制调整后的阈值位置
            float thresholdX = rect.x + adjustedThreshold * rect.width;
            Handles.color = new Color(1f, 0.5f, 0f, 0.8f); // 橙色
            Handles.DrawLine(
                new Vector3(thresholdX, rect.y), 
                new Vector3(thresholdX, rect.y + rect.height)
            );
            
            // 坐标轴
            Handles.color = Color.white;
            Handles.DrawLine(
                new Vector3(rect.x, rect.y + rect.height), 
                new Vector3(rect.x + rect.width, rect.y + rect.height)
            ); // X轴
            Handles.DrawLine(
                new Vector3(rect.x, rect.y), 
                new Vector3(rect.x, rect.y + rect.height)
            ); // Y轴
            
            // 绘制标签
            GUI.Label(new Rect(rect.x + rect.width - 40, rect.y + rect.height - 15, 40, 15), "亮度");
            GUI.Label(new Rect(rect.x, rect.y, 40, 15), "概率");
            GUI.Label(new Rect(rect.x, rect.y + rect.height - 15, 20, 15), "0");
            GUI.Label(new Rect(rect.x + rect.width - 20, rect.y + rect.height - 15, 20, 15), "1");
            
            // 绘制曲线
            Handles.color = Color.green;
            Vector3 prevPoint = Vector3.zero;
            int segments = 100;
            
            for (int i = 0; i <= segments; i++)
            {
                float x = (float)i / segments;
                float brightness = x;
                
                // 使用与Plant.cs相同的公式计算概率
                float probability = 1f / (1f + Mathf.Exp(-plant.bloomSteepness * (brightness - adjustedThreshold)));
                
                // 转换为屏幕坐标
                float screenX = rect.x + x * rect.width;
                float screenY = rect.y + rect.height - probability * rect.height;
                
                Vector3 point = new Vector3(screenX, screenY, 0);
                
                if (i > 0)
                {
                    Handles.DrawLine(prevPoint, point);
                }
                
                prevPoint = point;
            }
            
            // 在阈值处标记0.5概率点
            float thresholdY = rect.y + rect.height - 0.5f * rect.height;
            Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
            Handles.DrawLine(
                new Vector3(thresholdX - 5, thresholdY), 
                new Vector3(thresholdX + 5, thresholdY)
            );
            
            // 显示阈值
            GUI.Label(
                new Rect(thresholdX - 25, thresholdY - 15, 50, 15), 
                adjustedThreshold.ToString("F2"),
                new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.5f, 0f) } }
            );
        }
        
        // 添加当前亮度和概率标记方法
        private void DrawCurrentBrightnessMarker(Rect rect, Plant plant)
        {
            if (plant.BrightnessRatio <= 0) return;
            
            // 计算当前亮度比例对应的位置
            float currentX = rect.x + plant.BrightnessRatio * rect.width;
            float currentY = rect.y + rect.height - plant.BloomProbability * rect.height;
            
            // 绘制垂直线标记当前亮度
            Handles.color = Color.cyan;
            Handles.DrawLine(
                new Vector3(currentX, rect.y + rect.height),
                new Vector3(currentX, currentY)
            );
            
            // 绘制水平线标记当前概率
            Handles.DrawLine(
                new Vector3(rect.x, currentY),
                new Vector3(currentX, currentY)
            );
            
            // 绘制当前点
            Handles.color = Color.yellow;
            Handles.DrawSolidDisc(new Vector3(currentX, currentY, 0), Vector3.forward, 5f);
            
            // 显示具体数值标签
            GUI.Label(
                new Rect(currentX - 25, currentY - 20, 50, 15),
                $"({plant.BrightnessRatio:F2}, {plant.BloomProbability:F2})",
                new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } }
            );
        }
    }
#endif
} 