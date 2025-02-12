using UnityEngine;

[RequireComponent(typeof(CustomCollider))]
public class ScaleByBounds : MonoBehaviour
{
    private CustomCollider customCollider;

    void Start()
    {
        customCollider = GetComponent<CustomCollider>();

    }

    void Update()
    {

        // 确保物体的y值等于LightHeight*MinNodeSize.x
        transform.position = new Vector3(
            transform.position.x, 
            customCollider.Bounds.size.y, 
            transform.position.z
        );
       
    }
} 