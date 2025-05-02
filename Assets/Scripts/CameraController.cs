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
    public float scrollValue; // 新增显示scroll值的公开属性
    public float fixedAngleWithXZ = 45f; // 摄像机-玩家连线与XZ平面的固定夹角
    public bool useFixedAngle = true; // 是否使用固定夹角模式
    
    // 新增变量，用于处理窗口焦点变化
    private bool hasFocus = true;
    private float lastFocusChangeTime = 0f;
    private float focusChangeCooldown = 0.5f; // 焦点变化后的冷却时间

    private Vector3 rotation = Vector3.zero;
    private Vector3 currentRotation;
    private Vector3 velocity = Vector3.zero;
    private float currentZoom;

    // 保存相机设置的键名
    private const string ROTATION_X_KEY = "CameraRotationX";
    private const string ROTATION_Y_KEY = "CameraRotationY";
    private const string ROTATION_Z_KEY = "CameraRotationZ";
    private const string ZOOM_KEY = "CameraZoom";

    void Start()
    {
        // 加载保存的相机设置
        LoadCameraSettings();
    }

    void OnApplicationFocus(bool focusStatus)
    {
        hasFocus = focusStatus;
        lastFocusChangeTime = Time.time;
    }

    void Update()
    {
        // 鼠标右键拖动旋转
        if(target==null){
            return;
        }
        if (Input.GetMouseButton(1))
        {
            if (useFixedAngle)
            {
                // 固定夹角模式下只允许水平旋转
                rotation.y += Input.GetAxis("Mouse X") * rotationSpeed;
            }
            else
            {
                // 正常模式
                rotation.x += Input.GetAxis("Mouse Y") * rotationSpeed;
                rotation.y += Input.GetAxis("Mouse X") * rotationSpeed;
                rotation.x = Mathf.Clamp(rotation.x, -80, 80); // 限制垂直旋转角度
            }
        }

        // 鼠标滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        scrollValue = scroll; // 更新公开属性
        
        // 检查窗口焦点变化后的冷却期
        bool inCooldownPeriod = (Time.time - lastFocusChangeTime) < focusChangeCooldown;
        
        // 只有在非冷却期或scroll为0时才应用缩放
        if (!inCooldownPeriod || Mathf.Approximately(scroll, 0f))
        {
            currentZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, minZoom, maxZoom);
        }

        // 平滑插值
        currentRotation = Vector3.SmoothDamp(currentRotation, rotation, ref velocity, smoothTime);
        float targetZoom = currentZoom; // 目标缩放值是通过上面鼠标滚轮输入计算出的
        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref velocity.z, smoothTime);

        // 计算新的位置和旋转
        if (useFixedAngle)
        {
            // 固定夹角模式
            // 1. 使用Y轴旋转计算水平方向
            Quaternion horizontalRot = Quaternion.Euler(0, currentRotation.y, 0);
            
            // 2. 计算基于固定夹角的位置
            float heightOffset = Mathf.Sin(fixedAngleWithXZ * Mathf.Deg2Rad) * currentZoom;
            float horizontalDistance = Mathf.Cos(fixedAngleWithXZ * Mathf.Deg2Rad) * currentZoom;
            
            // 3. 将水平距离转换为方向向量
            Vector3 horizontalDir = horizontalRot * new Vector3(0, 0, -horizontalDistance);
            
            // 4. 最终位置 = 目标位置 + 水平偏移 + 高度偏移
            transform.position = target.position + horizontalDir + new Vector3(0, heightOffset, 0);
        }
        else
        {
            // 原始模式
            Quaternion rot = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
            Vector3 dir = new Vector3(0, 0, -currentZoom);
            transform.position = target.position + rot * dir;
        }
        
        // 始终看向目标
        transform.LookAt(target.position);

        // 当鼠标停止操作一段时间后保存相机设置
        if (Input.GetMouseButtonUp(1) || Mathf.Abs(scroll) > 0)
        {
            SaveCameraSettings();
        }

        // 获取摄像机信息并传递给着色器
        if (this != null)
        {
            // 传递摄像机位置
            Shader.SetGlobalVector("_CameraWorldPos", transform.position);
            
            // 传递摄像机Y轴旋转角度
            Shader.SetGlobalFloat("_CameraRotationY", transform.eulerAngles.y);
            
            // 传递摄像机缩放值
            Shader.SetGlobalFloat("_CameraZoom", currentZoom);
        }
    }
    
    // 保存相机设置
    private void SaveCameraSettings()
    {
        PlayerPrefs.SetFloat(ROTATION_X_KEY, rotation.x);
        PlayerPrefs.SetFloat(ROTATION_Y_KEY, rotation.y);
        PlayerPrefs.SetFloat(ROTATION_Z_KEY, rotation.z);
        PlayerPrefs.SetFloat(ZOOM_KEY, currentZoom);
        PlayerPrefs.Save();
    }

    // 加载相机设置
    private void LoadCameraSettings()
    {
        // 检查是否有保存的设置，如果有则加载，否则使用默认值
        if (PlayerPrefs.HasKey(ZOOM_KEY))
        {
            rotation.x = PlayerPrefs.GetFloat(ROTATION_X_KEY, 0);
            rotation.y = PlayerPrefs.GetFloat(ROTATION_Y_KEY, 0);
            rotation.z = PlayerPrefs.GetFloat(ROTATION_Z_KEY, 0);
            currentRotation = rotation;
            currentZoom = PlayerPrefs.GetFloat(ZOOM_KEY, initialZoom);
        }
        else
        {
            // 使用初始缩放值
            currentZoom = initialZoom;
        }
    }
}