using UnityEngine;
using TMPro;
using Firebase.Auth;
using System.Threading.Tasks;
using System;
using System.Net.Mail;

public class PasswordResetHandler : MonoBehaviour
{
    [Header("UI Dependencies")]
    public TMP_InputField resetEmailInputField; // Ô nhập email cần reset
    public GameObject loadingPanel;             // Panel loading (Thường là LoadingPanelRoot)
    public TMP_Text statusText;                 // Text thông báo trạng thái (Thường là Text trong Loading Panel)
    public GameObject resetPasswordPanel;       // Panel Reset Password (dùng để SetPanelInteractable)

    // Hàm gọi khi người dùng nhấn nút "Gửi Email Khôi Phục"
    public async void OnSendPasswordResetEmailClicked()
    {
        string email = resetEmailInputField.text.Trim();

        void OnEnable()
        {
            ResetUI();
        }

        if (string.IsNullOrEmpty(email))
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng nhập email để đặt lại mật khẩu.");
            return;
        }

        if (!IsValidEmail(email))
        {
            StatusPopupManager.Instance.ShowPopup("Địa chỉ email không hợp lệ. Vui lòng kiểm tra lại.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng.");
            return;
        }

        if (statusText != null) statusText.text = "Đang gửi email khôi phục...";
        if (loadingPanel != null) loadingPanel.SetActive(true);
        SetPanelInteractable(resetPasswordPanel, false);

        // Đảm bảo UI cập nhật trước khi gọi Firebase
        await Task.Yield();

        try
        {
            await FirebaseAuth.DefaultInstance.SendPasswordResetEmailAsync(email);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (loadingPanel != null) loadingPanel.SetActive(false);

                // Ẩn panel reset sau khi gửi thành công
                if (resetPasswordPanel != null) resetPasswordPanel.SetActive(false);

                StatusPopupManager.Instance.ShowPopup(
                    $"Đã gửi email khôi phục mật khẩu đến: {email}. Vui lòng kiểm tra hộp thư (kể cả Spam/Junk).",
                             () =>
                                {
                                    AuthManager.Instance?.ShowLoginPanel();
                                }
                );
            });
        }
        catch (Exception ex)
        {
            Firebase.FirebaseException firebaseEx = ex.GetBaseException() as Firebase.FirebaseException;
            string errorMessage = "Lỗi gửi email khôi phục.";

            if (firebaseEx != null)
            {
                AuthError authError = (AuthError)firebaseEx.ErrorCode;
                switch (authError)
                {
                    case AuthError.UserNotFound:
                        // Thông báo chung chung để bảo mật
                        errorMessage = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi liên kết khôi phục. Vui lòng kiểm tra hộp thư của bạn.";
                        break;
                    case AuthError.NetworkRequestFailed:
                        errorMessage = "Lỗi kết nối mạng. Vui lòng kiểm tra Internet và thử lại.";
                        break;
                    default:
                        errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                        break;
                }
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (loadingPanel != null) loadingPanel.SetActive(false);
                SetPanelInteractable(resetPasswordPanel, true);
                StatusPopupManager.Instance.ShowPopup(errorMessage);
                if (statusText != null) statusText.text = "Gửi thất bại.";
            });
            Debug.LogError("PasswordReset: Lỗi khi gửi email khôi phục: " + ex.Message);
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (panel != null)
        {
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }

    // Reset UI khi mở panel
    public void ResetUI()
    {
        if (resetEmailInputField != null) resetEmailInputField.text = "";
        if (statusText != null) statusText.text = "";
        if (loadingPanel != null) loadingPanel.SetActive(false);
        SetPanelInteractable(resetPasswordPanel, true);
    }
}
