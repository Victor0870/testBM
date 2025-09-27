using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Cần thiết cho các loại lỗi (nếu cần)

// BẮT BUỘC: Script này phải kế thừa từ MonoBehaviour
[RequireComponent(typeof(Button))]
public class PasswordVisibilityToggle : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Kéo InputField mà nút này sẽ điều khiển vào đây.")]
    public TMP_InputField targetInputField;

    [Header("Icon Settings")]
    public Sprite eyeOpenSprite;     // Icon Mắt mở (Mật khẩu hiển thị)
    public Sprite eyeClosedSprite;    // Icon Mắt đóng (Mật khẩu ẩn)

    private Button toggleButton;
    private bool isPasswordVisible = false;

    void Awake()
    {
        toggleButton = GetComponent<Button>();
        if (toggleButton == null)
        {
            Debug.LogError("PasswordVisibilityToggle yêu cầu component Button.");
            return;
        }

        if (targetInputField == null)
        {
            Debug.LogError("Target InputField chưa được gán!");
            // Vô hiệu hóa nút nếu không có InputField để tránh lỗi
            toggleButton.interactable = false;
            return;
        }

        // Gắn hàm ToggleVisibility() vào sự kiện onClick của nút
        toggleButton.onClick.AddListener(ToggleVisibility);

        // Thiết lập trạng thái ban đầu: Ẩn và dùng icon mắt đóng
        SetInitialState();
    }

    private void SetInitialState()
    {
        isPasswordVisible = false;

        // Luôn đảm bảo kiểu nhập liệu ban đầu là Mật khẩu
        if (targetInputField.contentType != TMP_InputField.ContentType.Password)
        {
            targetInputField.contentType = TMP_InputField.ContentType.Password;
        }

        UpdateImage();
        // Cập nhật lại caret position (để InputField redraw)
        targetInputField.ForceLabelUpdate();
        targetInputField.caretPosition = targetInputField.text.Length;
    }

    public void ToggleVisibility()
    {
        if (targetInputField == null) return;

        isPasswordVisible = !isPasswordVisible;

        if (isPasswordVisible)
        {
            // Hiển thị mật khẩu: chuyển ContentType sang Standard
            targetInputField.contentType = TMP_InputField.ContentType.Standard;
        }
        else
        {
            // Ẩn mật khẩu: chuyển ContentType sang Password
            targetInputField.contentType = TMP_InputField.ContentType.Password;
        }

        UpdateImage();

        // Quan trọng: Đặt lại caret position để hiển thị đúng và buộc InputField vẽ lại (redraw)
        targetInputField.ForceLabelUpdate();
        targetInputField.caretPosition = targetInputField.text.Length;
    }

    private void UpdateImage()
    {
        Image buttonImage = toggleButton.GetComponent<Image>();
        if (buttonImage != null && eyeOpenSprite != null && eyeClosedSprite != null)
        {
            // Chuyển đổi Sprite dựa trên trạng thái hiển thị
            buttonImage.sprite = isPasswordVisible ? eyeOpenSprite : eyeClosedSprite;
        }
    }
}