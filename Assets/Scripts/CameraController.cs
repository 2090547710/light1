using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;       // 要围绕的目标物体
    public float rotationSpeed = 5f;
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 50f;
    public float smoothTime = 0.3f;
    public float initialZoom = 10f;  // 新增初始缩放参数

    private Vector3 rotation = Vector3.zero;
    private Vector3 currentRotation;
    private Vector3 velocity = Vector3.zero;
    private float currentZoom;

    void Start()
    {
        rotation = transform.eulerAngles;
        currentRotation = rotation;
        
        // 修改初始化方式：使用预设初始值代替自动计算
        currentZoom = initialZoom;
        
        // 根据初始值强制更新摄像机位置
        Quaternion initialRot = Quaternion.Euler(rotation.x, rotation.y, 0);
        Vector3 initialDir = new Vector3(0, 0, -initialZoom);
        transform.position = target.position + initialRot * initialDir;
        transform.LookAt(target.position);
    }

    void Update()
    {
        // 鼠标右键拖动旋转
        if (Input.GetMouseButton(0))
        {
            rotation.x += Input.GetAxis("Mouse Y") * rotationSpeed;
            rotation.y += Input.GetAxis("Mouse X") * rotationSpeed;
            rotation.x = Mathf.Clamp(rotation.x, -80, 80); // 限制垂直旋转角度
        }

        // 鼠标滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, minZoom, maxZoom);

        // 平滑插值
        currentRotation = Vector3.SmoothDamp(currentRotation, rotation, ref velocity, smoothTime);
        currentZoom = Mathf.SmoothDamp(currentZoom, currentZoom, ref velocity.z, smoothTime);

        // 计算新的位置和旋转
        Quaternion rot = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
        Vector3 dir = new Vector3(0, 0, -currentZoom);
        transform.position = target.position + rot * dir;
        
        // 始终看向目标
        transform.LookAt(target.position);
    }
}