using UnityEngine;
using UnityEngine.UI;

public class PrivacyPolicyManager : MonoBehaviour
{
    [Header("Settings")]
    // Dán link bạn vừa tạo vào đây trong Inspector, hoặc sửa trực tiếp ở đây
     public string privacyPolicyUrl = "https://docs.google.com/document/d/1vTtm4zqrrueBGSkQF-ue2UAk-Rpq_aiOiAb45NEyySM/preview";

    [Header("UI References")]
    public Button openPolicyButton;

    void Start()
    {
        if (openPolicyButton != null)
        {
            openPolicyButton.onClick.AddListener(OpenPolicyLink);
        }
    }

    public void OpenPolicyLink()
    {
        if (!string.IsNullOrEmpty(privacyPolicyUrl))
        {
            Application.OpenURL(privacyPolicyUrl);
            Debug.Log("Đang mở chính sách bảo mật: " + privacyPolicyUrl);
        }
        else
        {
            Debug.LogError("Chưa thiết lập đường link Chính sách bảo mật!");
        }
    }
}