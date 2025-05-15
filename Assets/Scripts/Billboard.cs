using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer[] childSprites;
    private bool isFlipped = false;
    
    void Start()
    {
        mainCamera = Camera.main;
        // 获取所有子对象的SpriteRenderer组件
        childSprites = GetComponentsInChildren<SpriteRenderer>(true);
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
    
    // 翻转所有子对象的Sprite
    public void FlipSprites(bool flip)
    {
        if (isFlipped != flip)
        {
            isFlipped = flip;
            foreach (SpriteRenderer sprite in childSprites)
            {
                if (sprite != null)
                {
                    sprite.flipX = flip;
                }
            }
        }
    }
    
    // 重置翻转状态（将翻转设置为false）
    public void ResetFlip()
    {
        if (isFlipped)
        {
            FlipSprites(false);
        }
    }
    
    // 获取当前翻转状态
    public bool IsFlipped()
    {
        return isFlipped;
    }
} 