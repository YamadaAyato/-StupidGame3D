using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;



public class CurrentSpeedUI : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TMP_Text _speedText;

    private Tween _scaleText;
    
    void Update()
    {
        float zSpeed = playerController.CurrentZSpeed;
        _speedText.text = zSpeed.ToString("F2");
        
        float targetScale = Mathf.Clamp(1f +  Mathf.Abs(zSpeed) / 20f, 1f, 1.5f);

        _scaleText?.Kill();
        _scaleText = _speedText.rectTransform
            .DOScale(targetScale, 0.2f)
            .SetEase(Ease.InQuad);
    }
}