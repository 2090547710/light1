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
            editingProperties.heightMap = lighting.heightMap;
            editingProperties.tiling = lighting.tiling;
            editingProperties.offset = lighting.offset;
            editingProperties.lightHeight = lighting.lightHeight;
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
            if (selector.SelectedObject != null && 
                selector.SelectedObject.TryGetComponent<Lighting>(out var lighting))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("选中的光照属性", EditorStyles.boldLabel);
                
                // 创建可撤销的修改记录
                Undo.RecordObject(lighting, "Modify Lighting Properties");
                
                // 同步编辑属性到实际组件
                selector.editingProperties.size = EditorGUILayout.FloatField("Size", selector.editingProperties.size);
                selector.editingProperties.isObstacle = EditorGUILayout.Toggle("Is Obstacle", selector.editingProperties.isObstacle);
                selector.editingProperties.heightMap = (Texture2D)EditorGUILayout.ObjectField("Height Map", selector.editingProperties.heightMap, typeof(Texture2D), false);
                selector.editingProperties.tiling = EditorGUILayout.Vector2Field("Tiling", selector.editingProperties.tiling);
                selector.editingProperties.offset = EditorGUILayout.Vector2Field("Offset", selector.editingProperties.offset);
                selector.editingProperties.lightHeight = EditorGUILayout.Slider("Light Height", selector.editingProperties.lightHeight, 0, 1);

                // 应用修改到实际组件
                lighting.size = selector.editingProperties.size;
                lighting.isObstacle = selector.editingProperties.isObstacle;
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
        }
    }
#endif
} 