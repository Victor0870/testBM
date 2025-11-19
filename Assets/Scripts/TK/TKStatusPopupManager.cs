// File: Scripts/TK/TKStatusPopupManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using System;

// KHÔNG CÒN KẾ THỪA TỪ MonoBehaviour CŨ
public class TKStatusPopupManager : MonoBehaviour
{
    // Singleton Instance (Thay thế Instance của StatusPopupManager cũ)
    public static TKStatusPopupManager Instance { get; private set; }

    [Header("UI Toolkit Assets")]
    public VisualTreeAsset popupUxml; // Kéo file UXML cho popup vào đây
    public UIDocument targetUIDocument; // UIDocument của Scene hiện tại (TKLogin)

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }

        if (popupUxml == null) Debug.LogError("TKStatusPopupManager: popupUxml chưa được gán!");
        if (targetUIDocument == null)
        {
            // Nếu không được gán, cố gắng tìm UIDocument trong Scene
            targetUIDocument = FindObjectOfType<UIDocument>();
            if (targetUIDocument == null) Debug.LogError("TKStatusPopupManager: Không tìm thấy UIDocument trong Scene.");
        }
    }

    // Phương thức ShowPopup (Giữ nguyên chữ ký)
    public VisualElement ShowPopup(string message, Action callback = null)
    {
        if (popupUxml == null || targetUIDocument == null)
        {
            Debug.LogError("TKStatusPopupManager: Không thể tạo popup.");
            return null;
        }

        // 1. Tạo Popup từ UXML
        VisualElement popupRoot = popupUxml.Instantiate();

        // 2. Ánh xạ các phần tử
        Label messageText = popupRoot.Q<Label>("popup-message");
        Button okButton = popupRoot.Q<Button>("popup-ok-button");

        // 3. Thiết lập nội dung
        if (messageText != null) messageText.text = message;

        // 4. Gán Listener và hiển thị
        okButton?.RegisterCallback<ClickEvent>((evt) => {
            callback?.Invoke();
            // Xóa popup khỏi cây VisualElement
            popupRoot.RemoveFromHierarchy();
        });

        // 5. Thêm popup vào Root Visual Element của Scene
        targetUIDocument.rootVisualElement.Add(popupRoot);
        // Đảm bảo popup luôn ở trên cùng (SetAsLastChild không tồn tại trong UIElements)
        // Thay thế bằng cách thêm vào cuối cùng (Add)

        Debug.Log($"TKStatusPopupManager: Đã tạo và hiển thị popup: '{message}'");
        return popupRoot;
    }

    // Cần tạo một file UXML mới cho Popup (ví dụ: TKStatusPopup.uxml)
}