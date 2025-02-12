using UnityEngine;

[RequireComponent(typeof(Transform))]
public class CustomCollider : MonoBehaviour
{
    [SerializeField] 
    [Tooltip("XZ平面尺寸（以MinNodeSize为单位）")]
    private Vector2 _sizeXZ = Vector2.one;
    
    [SerializeField]
    [Tooltip("垂直高度（以MinNodeSize.x为单位）")]
    private float _heightY = 1f;

    public Vector2 MinNodeSize;


    void Start()
    {
        MinNodeSize=GameManager.MinNodeSize;
    }
    
    

    // 公开的边界属性
    public Bounds Bounds {
        get {
            Vector3 center = new Vector3(
                transform.position.x,
                0,
                transform.position.z
            );
            
            // 将单位尺寸转换为实际尺寸
            Vector3 size = new Vector3(
                _sizeXZ.x * MinNodeSize.x,    // X轴实际尺寸
                _heightY * MinNodeSize.x,     // Y轴高度使用MinNodeSize.x作为单位
                _sizeXZ.y * MinNodeSize.x      // Z轴实际尺寸
            );
            
            return new Bounds(center, size);
        }
    }

    // 属性访问器
    [Tooltip("XZ平面尺寸（以MinNodeSize为单位）")]
    public Vector2 UnitSizeXZ {
        get => _sizeXZ;
        set => _sizeXZ = value;
    }

    [Tooltip("垂直高度（以MinNodeSize.x为单位）")]
    public float UnitHeightY {
        get => _heightY;
        set => _heightY = value;
    }
}