using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequentialAnimator : MonoBehaviour
{
    public Animator[] imageAnimators;
    public float delayBetweenImages = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        // 开始时隐藏所有动画对象
        foreach (Animator animator in imageAnimators)
        {
            if (animator != null && animator.gameObject != null)
            {
                animator.gameObject.SetActive(false);
            }
        }
        
        // 订阅交互事件
        PlantInteraction.OnInteractiveObjectClicked += OnInteractiveObjectClicked;
    }
    
    // 事件响应方法
    private void OnInteractiveObjectClicked()
    {
        ShowAllImages();
    }
    

    // 当对象被销毁时取消订阅
    private void OnDestroy()
    {
        PlantInteraction.OnInteractiveObjectClicked -= OnInteractiveObjectClicked;
    }
    
    // 显示所有图片的公共方法
    public void ShowAllImages()
    {
        StartCoroutine(PlayAnimationsSequentially());
    }
    
    // 使用协程顺序播放所有动画
    IEnumerator PlayAnimationsSequentially()
    {
        for (int i = 0; i < imageAnimators.Length; i++)
        {
            if (imageAnimators[i] != null)
            {
                // 播放当前动画
                PlayAnimationAt(i);
                
                // 等待指定时间
                yield return new WaitForSeconds(delayBetweenImages);
            }
        }
    }
    
    void PlayAnimationAt(int index)
    {
        if (index < imageAnimators.Length && imageAnimators[index] != null)
        {
            imageAnimators[index].gameObject.SetActive(true);
            imageAnimators[index].SetTrigger("Show");
        }
    }
    
    public void HideAllImages()
    {
        for (int i = imageAnimators.Length - 1; i >= 0; i--)
        {
            if (imageAnimators[i].gameObject.activeSelf)
            {
                imageAnimators[i].SetTrigger("Hide");
            }
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
