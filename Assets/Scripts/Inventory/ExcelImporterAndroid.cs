// File: ExcelImporterAndroid.cs
using UnityEngine;
using NativeFilePickerNamespace;
using System.IO;
using BansheeGz.BGDatabase;

public class ExcelImporterAndroid : MonoBehaviour
{
    public BGExcelImportGo importComponent;
    public GameObject loadingPanel;

    private StatusPopupInstance currentLoadingPopup; // <-- MỚI: Để lưu tham chiếu popup "Đang nhập..."

    void Start()
    {
        if (importComponent == null) Debug.LogError("Import Component (BGExcelImportGo) is not assigned.");
        if (importComponent != null)
        {
            importComponent.OnImportUnityEvent.AddListener(OnImportCompleted);
        }
    }

    public void SelectAndImportExcel()
    {
        OpenFilePicker();
    }

    private void OpenFilePicker()
    {
        string[] fileTypes = new string[] { "xlsx", "xls" };

        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                Debug.Log("User cancelled file picker");
                StatusPopupManager.Instance.ShowPopup("Đã hủy chọn file Excel.");
                if (loadingPanel != null) loadingPanel.SetActive(false);
                return;
            }

            Debug.Log("Excel file selected: " + path);
            // Lưu tham chiếu đến popup "Đang nhập..."
            currentLoadingPopup = StatusPopupManager.Instance.ShowPopup("Đang nhập tồn kho từ Excel..."); // <-- LƯU THAM CHIẾU
            if (loadingPanel != null) loadingPanel.SetActive(true);

            if (importComponent != null)
            {
                importComponent.ExcelFile = path;
                importComponent.Import();
            }
            else
            {
                StatusPopupManager.Instance.ShowPopup("Lỗi: Thành phần nhập Excel không khả dụng.");
                if (loadingPanel != null) loadingPanel.SetActive(false);
                // Nếu có lỗi, đảm bảo popup "Đang nhập..." cũng được hủy
                if (currentLoadingPopup != null)
                {
                    Destroy(currentLoadingPopup.gameObject);
                    currentLoadingPopup = null;
                }
            }
        }, fileTypes);
    }

    private void OnImportCompleted()
    {
        // Nếu popup "Đang nhập..." còn tồn tại, hủy nó đi
        if (currentLoadingPopup != null)
        {
            Destroy(currentLoadingPopup.gameObject);
            currentLoadingPopup = null;
            Debug.Log("Đã đóng popup 'Đang nhập...' do nhập thành công.");
        }

        if (loadingPanel != null) loadingPanel.SetActive(false);
        StatusPopupManager.Instance.ShowPopup("Nhập tồn kho từ Excel thành công!");
    }
}