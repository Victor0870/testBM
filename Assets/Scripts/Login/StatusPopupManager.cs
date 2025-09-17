// File: StatusPopupManager.cs
using UnityEngine;
using System;

public class StatusPopupManager : MonoBehaviour
{
    // Singleton Instance
    public static StatusPopupManager Instance { get; private set; }

    [Header("Prefab for Status Popup")]
    public GameObject statusPopupPrefab; // Kéo Prefab của popup (chứa StatusPopupInstance) vào đây

    private Canvas mainCanvas; // Sẽ tìm Canvas chính trong Scene hiện tại

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

        // Đảm bảo prefab đã được gán
        if (statusPopupPrefab == null)
        {
            Debug.LogError("StatusPopupManager: statusPopupPrefab IS NULL! Vui lòng gán Prefab của popup vào Inspector.");
        }
    }

    void OnEnable()
    {
        // Tìm Canvas chính của Scene hiện tại
        mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("StatusPopupManager: Không tìm thấy Canvas chính trong Scene hiện tại. Popup sẽ không hiển thị.");
        }
    }

    // Sửa đổi phương thức ShowPopup để trả về StatusPopupInstance
    public StatusPopupInstance ShowPopup(string message, Action callback = null) // <-- Thay đổi kiểu trả về
    {
        if (statusPopupPrefab == null)
        {
            Debug.LogError("StatusPopupManager: Không thể tạo popup vì statusPopupPrefab là NULL.");
            return null; // Trả về null nếu không tạo được
        }

        if (mainCanvas == null)
        {
            // Thử tìm lại Canvas nếu chưa có (ví dụ: sau khi tải scene mới)
            mainCanvas = FindAnyObjectByType<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogError("StatusPopupManager: Không tìm thấy Canvas để đặt popup. Popup không thể hiển thị.");
                return null; // Trả về null
            }
        }

        // Instantiate một object popup mới từ prefab
        GameObject newPopupGO = Instantiate(statusPopupPrefab, mainCanvas.transform);
        // Đặt vị trí cục bộ về 0,0,0 và scale về 1,1,1
        newPopupGO.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        newPopupGO.transform.localScale = Vector3.one;

        // Lấy script StatusPopupInstance từ object mới tạo
        StatusPopupInstance popupInstance = newPopupGO.GetComponent<StatusPopupInstance>();

        if (popupInstance != null)
        {
            popupInstance.SetupPopup(message, callback);
            // Đảm bảo popup mới tạo hiển thị trên cùng (nếu có nhiều popup chồng lên nhau)
            newPopupGO.transform.SetAsLastSibling();
            Debug.Log($"StatusPopupManager: Đã tạo và hiển thị popup mới với tin nhắn: '{message}'");
            return popupInstance; // <-- Trả về instance của popup
        }
        else
        {
            Debug.LogError("StatusPopupManager: Prefab statusPopupPrefab thiếu script StatusPopupInstance.");
            Destroy(newPopupGO); // Hủy bỏ nếu không đúng cấu trúc
            return null; // Trả về null
        }
    }

    // HidePopup() không còn cần thiết ở đây vì mỗi instance popup tự hủy
    // private void OnOkButtonClicked() cũng không còn ở đây
}