using UnityEngine;
using System.Linq;

public class ObjectSelector : MonoBehaviour
{
    public LayerMask selectableLayer;
    public float selectionRadius = 0.5f;
    public Material highlightMaterial;
    
    private GameObject selectedObject;
    private Material originalMaterial;

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
                new Vector3(selectionRadius, 0.1f, selectionRadius));
            
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
    }
} 