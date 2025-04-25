using UnityEngine;

public class XZSwayer : MonoBehaviour
{
    [Header("晃动设置")]
    [Tooltip("X轴方向的晃动幅度")]
    public float xAmplitude = 0.5f;
    [Tooltip("Z轴方向的晃动幅度")]
    public float zAmplitude = 0.5f;
    [Tooltip("X轴方向的晃动频率")]
    public float xFrequency = 1.0f;
    [Tooltip("Z轴方向的晃动频率")]
    public float zFrequency = 1.0f;
    [Tooltip("X轴方向的相位偏移")]
    public float xPhaseOffset = 0f;
    [Tooltip("Z轴方向的相位偏移")]
    public float zPhaseOffset = 1.57f; // 默认约π/2，使X和Z形成圆形运动

    [Header("随机设置")]
    [Tooltip("是否对每个物体使用随机初始相位")]
    public bool useRandomPhase = true;
    [Tooltip("是否对每个物体使用随机振幅修改器")]
    public bool useRandomAmplitude = false;
    [Range(0.5f, 1.5f)]
    [Tooltip("随机振幅修改范围")]
    public float amplitudeRandomRange = 1.0f;

    // 物体的初始位置
    private Vector3 initialPosition;
    // 随机相位偏移
    private float randomXPhase;
    private float randomZPhase;
    // 随机振幅修改器
    private float randomXAmplitudeModifier = 1.0f;
    private float randomZAmplitudeModifier = 1.0f;

    void Start()
    {
        // 记录初始位置
        initialPosition = transform.position;
        
        // 如果启用随机相位，为每个物体生成随机初始相位
        if (useRandomPhase)
        {
            randomXPhase = Random.Range(0f, Mathf.PI * 2);
            randomZPhase = Random.Range(0f, Mathf.PI * 2);
        }
        
        // 如果启用随机振幅，为每个物体生成随机振幅修改器
        if (useRandomAmplitude)
        {
            randomXAmplitudeModifier = Random.Range(1.0f / amplitudeRandomRange, amplitudeRandomRange);
            randomZAmplitudeModifier = Random.Range(1.0f / amplitudeRandomRange, amplitudeRandomRange);
        }
    }

    void Update()
    {
        // 计算X轴晃动值
        float xOffset = Mathf.Sin(Time.time * xFrequency + xPhaseOffset + randomXPhase) * xAmplitude * randomXAmplitudeModifier;
        
        // 计算Z轴晃动值
        float zOffset = Mathf.Sin(Time.time * zFrequency + zPhaseOffset + randomZPhase) * zAmplitude * randomZAmplitudeModifier;
        
        // 应用晃动到物体位置
        transform.position = new Vector3(
            initialPosition.x + xOffset,
            initialPosition.y,
            initialPosition.z + zOffset
        );
    }
} 