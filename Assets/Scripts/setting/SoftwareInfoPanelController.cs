// File: SoftwareInfoPanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using static ShopSettingManager; // Để sử dụng ShopData
using static ShopSessionData; // Truy cập AppPackageConfig và GlobalAppConfig

public class SoftwareInfoPanelController : MonoBehaviour
{
    [Header("Software Information UI")]
    public TMP_Text appVersionText;
    public TMP_Text appPackageConfigVersionText; // Phiên bản của asset PackageConfig
    public TMP_Text globalAppConfigFreeTrialDaysText; // Thời gian dùng thử từ Firestore
    public TMP_Text currentPackageNameText; // Gói đang dùng
    public TMP_Text licenseEndDateText; // Hạn sử dụng license (đã có ở ShopSettingManager, nhưng có thể hiển thị lại ở đây)

    // Software Info Panel thường không có Edit/Save/Cancel, trừ khi bạn muốn chỉnh sửa nó.
    // Nếu có, thêm public Button editButton, saveButton, cancelButton; và logic tương tự các panel khác.

    private ShopData _cachedShopData; // Dữ liệu shop hiện tại
    private bool _listenersInitialized = false;

    // SetupPanel được gọi từ ShopSettingManager
    public void SetupPanel(ShopData data, bool editMode = false) // Không cần onSaveCallback nếu không có nút Save
    {
        _cachedShopData = data;

        DisplayInfo();
        // SetEditMode(editMode); // Không cần nếu không có Edit/Save/Cancel
    }

    private void DisplayInfo()
    {
        if (appVersionText != null) appVersionText.text = $"Phiên bản ứng dụng: {Application.version}";

        if (appPackageConfigVersionText != null && AppPackageConfig != null)
        {
            // Nếu PackageConfig có một trường version, bạn có thể hiển thị ở đây
            appPackageConfigVersionText.text = $"Phiên bản cấu hình gói (Asset): N/A"; // Hiện tại không có version trong PackageConfig.cs
        }

        if (globalAppConfigFreeTrialDaysText != null && AuthManager.GlobalAppConfig != null)
        {
            globalAppConfigFreeTrialDaysText.text = $"Số ngày dùng thử miễn phí: {AuthManager.GlobalAppConfig.FreeTrialDurationDays} ngày";
        }

        if (currentPackageNameText != null && _cachedShopData != null)
        {
            currentPackageNameText.text = $"Gói hiện tại: {_cachedShopData.packageType ?? "Không xác định"}";
        }

        if (licenseEndDateText != null && _cachedShopData != null && _cachedShopData.licenseEndDate != null)
        {
            DateTime endDate = _cachedShopData.licenseEndDate.ToDateTime().ToLocalTime();
            licenseEndDateText.text = $"Hạn sử dụng license: {endDate:dd/MM/yyyy HH:mm}";
            // Hiển thị trạng thái hết hạn nếu cần
            if (endDate < DateTime.Now)
            {
                licenseEndDateText.text += " (Đã hết hạn)";
                licenseEndDateText.color = Color.red;
            }
            else
            {
                licenseEndDateText.color = Color.green;
            }
        }
        else if (licenseEndDateText != null)
        {
            licenseEndDateText.text = "Hạn sử dụng license: Không xác định";
            licenseEndDateText.color = Color.black;
        }
    }
    // Các hàm SetInteractableInputs, SetEditMode, OnEditButtonClicked, OnSaveButtonClicked, OnCancelEditButtonClicked
    // không cần thiết ở đây nếu panel này chỉ để hiển thị thông tin tĩnh.
}