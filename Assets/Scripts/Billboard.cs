using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
    }
    
    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // 使物体始终面向摄像机
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                            mainCamera.transform.rotation * Vector3.up);
        }
    }
} 