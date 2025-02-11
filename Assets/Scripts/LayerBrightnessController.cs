using UnityEngine;

public class LayerBrightnessController : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] [Range(0, 1)] private float brightness = 0.5f;
    
    private Material brightnessMaterial;

    void Start()
    {
        brightnessMaterial = new Material(Shader.Find("Custom/LayerBrightness"));
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        brightnessMaterial.SetFloat("_Brightness", brightness);
        brightnessMaterial.SetFloat("_TargetLayer", (float)targetLayers.value);
        Graphics.Blit(src, dest, brightnessMaterial);
    }
} 