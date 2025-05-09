using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ButtonShaker : MonoBehaviour
{
    [Header("抖动设置")]
    [Tooltip("抖动方向 (例如: 右方向为 Vector2(1,0))")]
    public Vector2 shakeDirection = Vector2.right;
    
    [Tooltip("抖动幅度")]
    public float shakeAmount = 10f;
    
    [Tooltip("抖动次数")]
    public int shakeCount = 3;
    
    [Tooltip("抖动持续时间")]
    public float shakeDuration = 0.3f;
    
    [Header("键盘触发设置")]
    [Tooltip("是否启用键盘触发")]
    public bool enableKeyboardTrigger = true;
    
    [Tooltip("触发抖动的键盘按键")]
    public KeyCode triggerKey = KeyCode.None;
    
    // 按钮组件引用
    private Button button;
    // 按钮的RectTransform
    private RectTransform rectTransform;
    // 按钮的原始位置
    private Vector2 originalPosition;
    // 是否正在抖动
    private bool isShaking = false;
    
    void Awake()
    {
        // 获取按钮组件和RectTransform
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        
        // 如果找到按钮，添加点击监听
        if (button != null)
        {
            // 保存原始委托，并添加抖动效果
            button.onClick.AddListener(StartShake);
        }
    }
    
    // 开始抖动
    public void StartShake()
    {
        // 如果没有在抖动，开始抖动
        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine());
        }
    }
    
    // 抖动协程
    private IEnumerator ShakeCoroutine()
    {
        isShaking = true;
        
        // 保存原始位置
        originalPosition = rectTransform.anchoredPosition;
        
        // 归一化抖动方向
        Vector2 direction = shakeDirection.normalized;
        
        float timePerShake = shakeDuration / (shakeCount * 2);
        
        for (int i = 0; i < shakeCount; i++)
        {
            // 向指定方向抖动
            float elapsed = 0f;
            while (elapsed < timePerShake)
            {
                float t = elapsed / timePerShake;
                float offset = Mathf.Sin(t * Mathf.PI) * shakeAmount;
                rectTransform.anchoredPosition = originalPosition + direction * offset;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 向反方向抖动回来
            elapsed = 0f;
            while (elapsed < timePerShake)
            {
                float t = elapsed / timePerShake;
                float offset = Mathf.Sin(t * Mathf.PI) * shakeAmount;
                rectTransform.anchoredPosition = originalPosition - direction * offset;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        
        // 确保回到原始位置
        rectTransform.anchoredPosition = originalPosition;
        isShaking = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        // 记录初始位置
        originalPosition = rectTransform.anchoredPosition;
    }

    // Update is called once per frame
    void Update()
    {
        // 检测键盘输入
        if (enableKeyboardTrigger && Input.GetKeyDown(triggerKey) && triggerKey != KeyCode.None)
        {
            StartShake();
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(StartShake);
        }
    }
}
