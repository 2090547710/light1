using UnityEngine;
using System.Linq;
using UnityEditor;

public class ObjectSelector : MonoBehaviour
{
    public LayerMask selectableLayer;
    public float selectionRadius = 2f;   
    private GameObject selectedObject;


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
        public float rotation;
        [Range(0, 1)] public float lightHeight;
    }

    [Header("光照属性编辑")]
    [SerializeField] private LightingProperties editingProperties = new LightingProperties();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            // 只使用射线检测选择
            TryRaycastSelection();
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

    void HandleSelection(GameObject obj)
    {
        // 查找有Lighting组件的对象，从当前对象开始，依次向上查找父对象
        GameObject currentObj = obj;
        GameObject lightingObj = null;
        
        // 循环向上查找，直到找到带有Lighting组件的对象或者到达根节点
        while (currentObj != null)
        {
            if (currentObj.TryGetComponent<Lighting>(out var lighting))
            {
                lightingObj = currentObj;
                break;
            }
            currentObj = currentObj.transform.parent?.gameObject;
        }
        
        // 如果找到了带有Lighting组件的对象，将其设为选中对象
        // 否则使用默认行为（选择对象的父对象）
        selectedObject = lightingObj != null ? lightingObj : obj.transform.parent?.gameObject;
        
        if(selectedObject != null){
            Debug.Log(selectedObject.name);
        
            // 扩展选择事件
            SelectionChanged?.Invoke(selectedObject);

            // 同步选中对象的Lighting属性到编辑器
            if (selectedObject.TryGetComponent<Lighting>(out var lighting))
            {
                editingProperties.size = lighting.size;
                editingProperties.isObstacle = lighting.isObstacle;
                editingProperties.isSeed = lighting.isSeed;
                editingProperties.heightMap = lighting.heightMap;
                editingProperties.rotation = lighting.rotation;
                editingProperties.lightHeight = lighting.lightHeight;
            }
            
            // 选择对象后自动打开编辑器窗口
            #if UNITY_EDITOR
            ObjectPropertiesWindow.ShowWindow(this);
            #endif
        }
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
            if (selector.SelectedObject != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("选中对象的Transform", EditorStyles.boldLabel);
                
                // 记录对象以支持撤销
                Undo.RecordObject(selector.SelectedObject.transform, "修改Transform");
                
                // 编辑Position
                Vector3 newPosition = EditorGUILayout.Vector3Field("位置:", selector.SelectedObject.transform.position);
                if (newPosition != selector.SelectedObject.transform.position)
                {
                    selector.SelectedObject.transform.position = newPosition;
                    
                    // 如果有Lighting组件，标记为脏以便更新
                    if (selector.SelectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                    {
                        lightingComponent.MarkDirty();
                        if (Application.isPlaying)
                        {
                            lightingComponent.OnValidate();
                        }
                    }
                }
                
                // 编辑Rotation
                Vector3 currentRotation = selector.SelectedObject.transform.rotation.eulerAngles;
                Vector3 newRotation = EditorGUILayout.Vector3Field("旋转:", currentRotation);
                if (newRotation != currentRotation)
                {
                    selector.SelectedObject.transform.rotation = Quaternion.Euler(newRotation);
                    
                    // 如果有Lighting组件，标记为脏以便更新
                    if (selector.SelectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                    {
                        lightingComponent.MarkDirty();
                        if (Application.isPlaying)
                        {
                            lightingComponent.OnValidate();
                        }
                    }
                }
                
                // 编辑Scale
                Vector3 newScale = EditorGUILayout.Vector3Field("缩放:", selector.SelectedObject.transform.localScale);
                if (newScale != selector.SelectedObject.transform.localScale)
                {
                    selector.SelectedObject.transform.localScale = newScale;
                    
                    // 如果有Lighting组件，标记为脏以便更新
                    if (selector.SelectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                    {
                        lightingComponent.MarkDirty();
                        if (Application.isPlaying)
                        {
                            lightingComponent.OnValidate();
                        }
                    }
                }
                
                // 如果发生了变化，标记为脏对象
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(selector.SelectedObject.transform);
                }
            }
            
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
                    EditorGUILayout.HelpBox("Size必须大于0", UnityEditor.MessageType.Warning);
                }
                
                selector.editingProperties.isSeed = EditorGUILayout.Toggle("Is Seed", selector.editingProperties.isSeed);
                if(selector.editingProperties.isSeed && selector.editingProperties.isObstacle)
                {
                    selector.editingProperties.isObstacle = false;
                    EditorGUILayout.HelpBox("Seed不能同时是Obstacle", UnityEditor.MessageType.Warning);
                }
                selector.editingProperties.isObstacle = EditorGUILayout.Toggle("Is Obstacle", selector.editingProperties.isObstacle);
                if(selector.editingProperties.isObstacle && selector.editingProperties.isSeed)
                {
                    selector.editingProperties.isSeed = false;
                    EditorGUILayout.HelpBox("Obstacle不能同时是Seed", UnityEditor.MessageType.Warning);
                }
                selector.editingProperties.heightMap = (Texture2D)EditorGUILayout.ObjectField("Height Map", selector.editingProperties.heightMap, typeof(Texture2D), false);
                
                // 使用Slider控制旋转角度，范围为0-360度
                selector.editingProperties.rotation = EditorGUILayout.Slider("Rotation", selector.editingProperties.rotation, 0f, 360f);
                
                selector.editingProperties.lightHeight = EditorGUILayout.Slider("Light Height", selector.editingProperties.lightHeight, 0, 1);

                // 应用修改到实际组件
                lighting.size = Mathf.Max(0.01f, selector.editingProperties.size);
                lighting.isObstacle = selector.editingProperties.isObstacle;
                lighting.isSeed = selector.editingProperties.isSeed;
                lighting.heightMap = selector.editingProperties.heightMap;
                lighting.rotation = selector.editingProperties.rotation;
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
                EditorGUILayout.HelpBox("未选中有效的对象", UnityEditor.MessageType.Info);
                return;
            }
            
            GameObject selectedObject = targetSelector.SelectedObject;
            
            // 添加Transform编辑区域
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Transform编辑", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // 记录对象以支持撤销
            Undo.RecordObject(selectedObject.transform, "修改Transform");
            
            // 编辑Position
            Vector3 newPosition = EditorGUILayout.Vector3Field("位置:", selectedObject.transform.position);
            if (newPosition != selectedObject.transform.position)
            {
                selectedObject.transform.position = newPosition;
                
                // 如果有Lighting组件，标记为脏以便更新
                if (selectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                {
                    lightingComponent.MarkDirty();
                    
                    // 如果在运行时，立即更新光照
                    if (Application.isPlaying)
                    {
                        lightingComponent.OnValidate();
                    }
                }
            }
            
            // 编辑Rotation
            Vector3 currentRotation = selectedObject.transform.rotation.eulerAngles;
            Vector3 newRotation = EditorGUILayout.Vector3Field("旋转:", currentRotation);
            if (newRotation != currentRotation)
            {
                selectedObject.transform.rotation = Quaternion.Euler(newRotation);
                
                // 如果有Lighting组件，标记为脏以便更新
                if (selectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                {
                    lightingComponent.MarkDirty();
                    
                    // 如果在运行时，立即更新光照
                    if (Application.isPlaying)
                    {
                        lightingComponent.OnValidate();
                    }
                }
            }
            
            // 编辑Scale
            Vector3 newScale = EditorGUILayout.Vector3Field("缩放:", selectedObject.transform.localScale);
            if (newScale != selectedObject.transform.localScale)
            {
                selectedObject.transform.localScale = newScale;
                
                // 如果有Lighting组件，标记为脏以便更新
                if (selectedObject.TryGetComponent<Lighting>(out var lightingComponent))
                {
                    lightingComponent.MarkDirty();
                    
                    // 如果在运行时，立即更新光照
                    if (Application.isPlaying)
                    {
                        lightingComponent.OnValidate();
                    }
                }
            }
            
            // 如果发生了变化，标记为脏对象
            if (GUI.changed)
            {
                EditorUtility.SetDirty(selectedObject.transform);
            }
            
            // 添加分隔线
            EditorGUILayout.Space(10);
            Rect separatorRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(10);
            
            // 每次绘制界面前，同步数据（从对象读取最新值到编辑器）
            if (selectedObject.TryGetComponent<Lighting>(out var lighting))
            {
                targetSelector.editingProperties.size = lighting.size;
                targetSelector.editingProperties.isObstacle = lighting.isObstacle;
                targetSelector.editingProperties.isSeed = lighting.isSeed;
                targetSelector.editingProperties.heightMap = lighting.heightMap;
                targetSelector.editingProperties.rotation = lighting.rotation;
                targetSelector.editingProperties.lightHeight = lighting.lightHeight;
            }
            
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
                
                // 显示亮度比例
                EditorGUILayout.LabelField($"亮度比例: {plant.CalculateBrightnessRatio():F2}", EditorStyles.boldLabel);
                
                // 添加生长速度设置
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("生长速度设置", EditorStyles.boldLabel);
                
                // 记录GUI变更前的值
                float oldGrowthRate = plant.growthRate;
                
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
            // 获取所有Plant组件（包括继承自Plant的子类）
            var plantComponents = selectedObject.GetComponents<Plant>();
            
            if (plantComponents != null && plantComponents.Length > 0) {
                EditorGUILayout.Space();
                
                // 遍历所有Plant组件
                foreach (var plantComponent in plantComponents) {
                    // 检查该组件是否有光源
                    if (plantComponent.lightSources == null || plantComponent.lightSources.Count == 0) {
                        continue;
                    }
                    
                    // 检查是否为Fire类型并显示不同的标题
                    bool isFire = plantComponent is Fire;
                    string componentTypeName = plantComponent.GetType().Name;
                    
                    EditorGUILayout.LabelField($"{componentTypeName}光源组件列表编辑 ({plantComponent.plantName})", EditorStyles.boldLabel);

                    // 显示该Plant组件的所有光源
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
                                EditorGUILayout.HelpBox("种子不能同时为障碍物", UnityEditor.MessageType.Warning);
                            }
                        }

                        bool newIsObstacle = EditorGUILayout.Toggle("是否为障碍物:", lightElement.isObstacle);
                        if (newIsObstacle != lightElement.isObstacle) {
                            lightElement.isObstacle = newIsObstacle;
                            if (newIsObstacle && lightElement.isSeed) {
                                lightElement.isSeed = false;
                                EditorGUILayout.HelpBox("障碍物不能同时为种子", UnityEditor.MessageType.Warning);
                            }
                        }

                        float lightRotation = EditorGUILayout.Slider("旋转角度:", lightElement.rotation, 0f, 360f);
                        if (lightRotation != lightElement.rotation) {
                            lightElement.rotation = lightRotation;
                        }

                        float newLightHeight = EditorGUILayout.Slider("光照高度:", lightElement.lightHeight, 0, 1);
                        if (newLightHeight != lightElement.lightHeight) {
                            lightElement.lightHeight = newLightHeight;
                        }

                        Texture2D newHeightMap = (Texture2D)EditorGUILayout.ObjectField("高度图:", lightElement.heightMap, typeof(Texture2D), false);
                        if (newHeightMap != lightElement.heightMap) {
                            lightElement.heightMap = newHeightMap;
                        }

                        if (GUI.changed) {
                            EditorUtility.SetDirty(lightElement);
                            lightElement.OnValidate(); // 触发验证和更新
                        }
                        
                        EditorGUILayout.Space();
                    }
                    
                    // 为每个Plant组件之间添加分隔线
                    EditorGUILayout.Space(10);
                    Rect componentSeparatorRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(componentSeparatorRect, new Color(0.5f, 0.5f, 0.5f, 1));
                    EditorGUILayout.Space(10);
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

        private void Update()
        {
            // 定期检查所选对象的属性是否被其他地方修改，如果修改则刷新
            if (targetSelector != null && targetSelector.SelectedObject != null)
            {
                Repaint();
            }
        }
    }
#endif
} 