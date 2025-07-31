using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrentSpeedUI : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TMP_Text _speedText;
    
    void Update()
    {
        float zSpeed = playerController.CurrentZSpeed;
        _speedText.text = zSpeed.ToString("F2");
    }
}