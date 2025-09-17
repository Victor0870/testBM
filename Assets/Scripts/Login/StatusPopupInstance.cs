// File: StatusPopupInstance.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class StatusPopupInstance : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text messageText;
    public Button okButton;

    private Action onOkCallback;

    void Awake()
    {
        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkButtonClicked);
        }
        else
        {
            Debug.LogError("StatusPopupInstance: okButton IS NULL! Vui lòng gán nút OK trong Inspector.");
        }
    }

    // Phương thức để thiết lập thông báo và callback
    public void SetupPopup(string message, Action callback = null)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
        else
        {
            Debug.LogError("StatusPopupInstance: messageText chưa được gán! Không thể hiển thị tin nhắn.");
        }
        onOkCallback = callback;

        // Đảm bảo popup hiển thị
        gameObject.SetActive(true);
    }

    private void OnOkButtonClicked()
    {
        Debug.Log("StatusPopupInstance: Nút OK được bấm. Hủy bỏ popup.");
        onOkCallback?.Invoke(); // Gọi callback trước khi hủy

        // Hủy bỏ GameObject của popup này
        Destroy(this.gameObject);
    }
}