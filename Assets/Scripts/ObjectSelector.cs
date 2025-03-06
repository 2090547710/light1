using UnityEngine;
using System.Linq;
using UnityEditor;

public class ObjectSelector : MonoBehaviour
{
    public LayerMask selectableLayer;
    public float selectionRadius = 0.5f;
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
        
        // 触发选择事件（可扩展）
        Debug.Log($"Selected: {obj.name}");

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
            
            // 处理光照组件
            if (selectedObject.TryGetComponent<Lighting>(out var lighting))
            {
                EditorGUILayout.LabelField("光照属性编辑", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                // 创建可撤销的修改记录
                Undo.RecordObject(lighting, "Modify Lighting Properties");
                
                // 尺寸大小
                float newSize = EditorGUILayout.FloatField("大小:", lighting.size);
                if (newSize != lighting.size)
                {
                    lighting.size = Mathf.Max(0.01f, newSize);
                }
                
                // 是否为种子
                bool newIsSeed = EditorGUILayout.Toggle("是种子:", lighting.isSeed);
                if (newIsSeed != lighting.isSeed)
                {
                    lighting.isSeed = newIsSeed;
                    if (newIsSeed && lighting.isObstacle)
                    {
                        lighting.isObstacle = false;
                        EditorGUILayout.HelpBox("种子不能同时是障碍物", MessageType.Warning);
                    }
                }
                
                // 是否为障碍物
                bool newIsObstacle = EditorGUILayout.Toggle("是障碍物:", lighting.isObstacle);
                if (newIsObstacle != lighting.isObstacle)
                {
                    lighting.isObstacle = newIsObstacle;
                    if (newIsObstacle && lighting.isSeed)
                    {
                        lighting.isSeed = false;
                        EditorGUILayout.HelpBox("障碍物不能同时是种子", MessageType.Warning);
                    }
                }
                
                // 光照高度
                float newLightHeight = EditorGUILayout.Slider("光照高度:", lighting.lightHeight, 0, 1);
                if (newLightHeight != lighting.lightHeight)
                {
                    lighting.lightHeight = newLightHeight;
                }
                
                // 高度图
                Texture2D newHeightMap = (Texture2D)EditorGUILayout.ObjectField("高度图:", lighting.heightMap, typeof(Texture2D), false);
                if (newHeightMap != lighting.heightMap)
                {
                    lighting.heightMap = newHeightMap;
                }
                
                // 平铺
                Vector2 newTiling = EditorGUILayout.Vector2Field("平铺:", lighting.tiling);
                if (newTiling != lighting.tiling)
                {
                    lighting.tiling = newTiling;
                }
                
                // 偏移
                Vector2 newOffset = EditorGUILayout.Vector2Field("偏移:", lighting.offset);
                if (newOffset != lighting.offset)
                {
                    lighting.offset = newOffset;
                }
                
                // 亮度影响显示
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"亮度影响值: {lighting.TotalBrightnessImpact:F2}", 
                    new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold });
                
                // 立即应用修改
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(lighting);
                    lighting.OnValidate(); // 触发验证和更新
                }
                
                EditorGUILayout.Space(10);
            }
            
            // 处理植物组件
            if (selectedObject.TryGetComponent<Plant>(out var plant))
            {
                EditorGUILayout.LabelField("植物属性编辑", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                // 创建可撤销的修改记录
                Undo.RecordObject(plant, "Modify Plant Properties");
                
                // 显示当前生长阶段
                EditorGUILayout.LabelField($"当前阶段: {plant.currentStage}/{plant.maxStages}");
                
                // 显示是否枯萎
                EditorGUILayout.LabelField($"是否枯萎: {(plant.isWithered ? "是" : "否")}");
                
                // 显示亮度比例
                EditorGUILayout.LabelField($"亮度比例: {plant.BrightnessRatio:P2}");
                
                // 显示开花概率
                EditorGUILayout.LabelField($"开花概率: {plant.BloomProbability:P2}");
                
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
                
                // 计算开花概率按钮
                if (GUILayout.Button("计算概率", GUILayout.Height(30)))
                {
                    plant.CalculateBrightnessRatio();
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
    }
#endif
} 