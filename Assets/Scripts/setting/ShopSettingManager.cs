// File: ShopSettingManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using UnityEngine.SceneManagement;

using static ShopSessionData; // Để truy cập ShopSessionData.CachedShopSettings, AppPackageConfig, GlobalAppConfig

public class ShopSettingManager : MonoBehaviour
{
    // Khai báo một hằng số cho key trong PlayerPrefs
    private const string ENTER_SHOP_SETTING_EDIT_MODE_KEY = "EnterShopSettingEditMode";

    [Header("UI References - ShopSettingManager")]
    public TMP_Text accountNameText; // <-- MỚI: Text hiển thị tên tài khoản người dùng
    public Button shopInfoPanelHeaderButton; // Nút tiêu đề cho Panel Shop Information
    public Button eInvoicePanelHeaderButton; // Nút tiêu đề cho Panel E-Invoice Settings
    public Button packagePanelHeaderButton; // Nút tiêu đề cho Panel Subscription Package UI
    public Button softwareInfoPanelHeaderButton; // <-- MỚI: Nút tiêu đề cho Panel Software Info
    public RectTransform contentPanelContainer; // <-- MỚI: RectTransform cha để chứa các panel nội dung (Instantiated)

    [Header("Content Panel Prefabs - ShopSettingManager")]
    public GameObject shopInfoPanelPrefab; // Kéo Prefab ShopInfoPanel vào đây
    public GameObject eInvoicePanelPrefab; // Kéo Prefab EInvoicePanel vào đây
    public GameObject packagePanelPrefab;  // Kéo Prefab PackagePanel vào đây
    public GameObject softwareInfoPanelPrefab; // <-- MỚI: Kéo Prefab SoftwareInfoPanel vào đây

    [Header("License & Voucher UI - ShopSettingManager")]
    public TMP_Text licenseEndDateText; // Giữ nguyên, có thể cập nhật từ _cachedShopData
    public TMP_InputField voucherInputField; // Giữ nguyên
    public Button applyVoucherButton; // Giữ nguyên

    [Header("Control Buttons - ShopSettingManager")]
    // Nút Edit/Save/Cancel CŨ đã bị loại bỏ khỏi đây, chúng sẽ nằm trong các Panel con
    public Button goToHomepageButton; // Giữ nguyên
    public Button logoutButton; // Giữ nguyên

    [Header("Status & Loading")]
    public GameObject loadingPanel; // Giữ nguyên

    [Header("Scene References")]
    public Canvas mainCanvas;

    private FirebaseFirestore db;
    private FirebaseUser currentUser;
    private DocumentReference shopDocRef;

    private ShopData _cachedShopData; // Biến cục bộ để giữ dữ liệu shop hiện tại

    private GameObject _currentActiveContentPanel = null; // Tham chiếu đến panel nội dung đang hiển thị

    // Constants for Firestore collections/documents
    private const string SHOPS_COLLECTION = "shops";
    private const string VOUCHERS_COLLECTION = "vouchers";
    private const string APP_SETTINGS_COLLECTION = "app_settings";
    private const string PACKAGE_COSTS_DOC_ID = "package_costs";

    // --- Cấu trúc dữ liệu cho Shop (phải là public để ShopSessionData có thể truy cập) ---
    [FirestoreData]
    public class ShopData
    {
        [FirestoreProperty("shopName")] public string shopName { get; set; }
        [FirestoreProperty("phoneNumber")] public string phoneNumber { get; set; }
        [FirestoreProperty("taxId")] public string taxId { get; set; }
        [FirestoreProperty("industry")] public string industry { get; set; }

        [FirestoreProperty("eInvoiceProvider")] public string eInvoiceProvider { get; set; }
        [FirestoreProperty("eInvoiceUser")] public string eInvoiceUser { get; set; }
        [FirestoreProperty("eInvoicePass")] public string eInvoicePass { get; set; }
        [FirestoreProperty("invoiceSerial")] public string invoiceSerial { get; set; }
        [FirestoreProperty("invoiceForm")] public string invoiceForm { get; set; }
        [FirestoreProperty("invoiceType")] public string invoiceType { get; set; }

        [FirestoreProperty("fptAccessToken")] public string fptAccessToken { get; set; }
        [FirestoreProperty("fptTokenExpiryTime")] public long fptTokenExpiryTime { get; set; }

        [FirestoreProperty("licenseEndDate")] public Timestamp licenseEndDate { get; set; }
        [FirestoreProperty("packageType")] public string packageType { get; set; } // THÊM TRƯỜNG NÀY

        public ShopData() { }
    }
    // --- Kết thúc cấu trúc dữ liệu cho Shop ---

    void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth.DefaultInstance.StateChanged += AuthStateChanged;

        // Gán listener cho các nút Header để hiển thị Panel tương ứng
        shopInfoPanelHeaderButton?.onClick.AddListener(() => ShowContentPanel(shopInfoPanelPrefab, "Thông tin cửa hàng"));
        eInvoicePanelHeaderButton?.onClick.AddListener(() => ShowContentPanel(eInvoicePanelPrefab, "Thông tin hóa đơn điện tử"));
        packagePanelHeaderButton?.onClick.AddListener(() => ShowContentPanel(packagePanelPrefab, "Thông tin gói sử dụng"));
        softwareInfoPanelHeaderButton?.onClick.AddListener(() => ShowContentPanel(softwareInfoPanelPrefab, "Thông tin phần mềm")); // <-- MỚI

        applyVoucherButton?.onClick.AddListener(OnApplyVoucherButtonClicked);
        goToHomepageButton?.onClick.AddListener(OnGoToHomepageButtonClicked);
        logoutButton?.onClick.AddListener(OnLogoutButtonClicked);

        loadingPanel?.SetActive(false); // Đảm bảo loading panel ẩn ban đầu
        AuthStateChanged(this, null); // Gọi AuthStateChanged ban đầu để xử lý trạng thái user
    }

    void OnDestroy()
    {
        FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
    }

    void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != currentUser)
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentUser != null)
            {
                Debug.Log($"ShopSettingManager: User logged in: {currentUser.Email}");
                shopDocRef = db.Collection(SHOPS_COLLECTION).Document(currentUser.UserId);
                if (accountNameText != null) accountNameText.text = $"Tài khoản: {currentUser.Email}"; // Cập nhật tên tài khoản
                LoadSettingsAndShowDefaultPanel(); // Tải cài đặt và hiển thị panel mặc định
            }
            else
            {
                Debug.Log("ShopSettingManager: User logged out.");
                shopDocRef = null;
                if (accountNameText != null) accountNameText.text = "Tài khoản: (Chưa đăng nhập)";
                // Xóa panel đang hiển thị nếu user log out
                if (_currentActiveContentPanel != null)
                {
                    Destroy(_currentActiveContentPanel);
                    _currentActiveContentPanel = null;
                }
            }
        }
    }

    // Tải cài đặt shop và hiển thị panel mặc định
    private async void LoadSettingsAndShowDefaultPanel()
    {
        await LoadShopDataFromFirebase(); // Tải dữ liệu shop và cập nhật _cachedShopData

        // Kiểm tra xem có cần vào chế độ chỉnh sửa ban đầu không (ví dụ: shop mới tạo hoặc hết hạn license)
        bool enterEditModeImmediately = false;
        if (PlayerPrefs.HasKey(ENTER_SHOP_SETTING_EDIT_MODE_KEY) && PlayerPrefs.GetInt(ENTER_SHOP_SETTING_EDIT_MODE_KEY) == 1)
        {
            enterEditModeImmediately = true;
            PlayerPrefs.DeleteKey(ENTER_SHOP_SETTING_EDIT_MODE_KEY);
            PlayerPrefs.Save();
        }

        // Luôn hiển thị panel thông tin cửa hàng mặc định
        //ShowContentPanel(shopInfoPanelPrefab, "Thông tin cửa hàng", enterEditModeImmediately);

        // Hiển thị trạng thái license
        UpdateLicenseDisplay();
        CheckLicenseAndSetHomepageButtonState();

        loadingPanel?.SetActive(false);
    }

    // Phương thức tải dữ liệu shop từ Firebase hoặc cache
    private async Task<ShopData> LoadShopDataFromFirebase()
    {
        if (currentUser == null)
        {
            Debug.LogWarning("ShopSettingManager: currentUser is null. Cannot load shop data.");
            return null;
        }

        loadingPanel?.SetActive(true);

        if (ShopSessionData.CachedUserId == currentUser.UserId && ShopSessionData.CachedShopSettings != null)
        {
            _cachedShopData = ShopSessionData.CachedShopSettings;
            Debug.Log("ShopSettingManager: Loading shop settings from cache.");
        }
        else
        {
            Debug.Log("ShopSettingManager: Cached data invalid or not found. Loading from Firebase.");
            try
            {
                DocumentSnapshot snapshot = await shopDocRef.GetSnapshotAsync();
                ShopData shopData = null;
                if (snapshot.Exists)
                {
                    shopData = snapshot.ConvertTo<ShopData>();
                    Debug.Log("Shop data loaded from Firebase.");
                    if (string.IsNullOrEmpty(shopData.packageType))
                    {
                        Debug.LogWarning($"LoadShopDataFromFirebase: PackageType cho user {currentUser.UserId} bị thiếu hoặc rỗng. Đặt lại thành mặc định 'Basic'.");
                        shopData.packageType = "Basic";
                    }
                }
                else
                {
                    Debug.Log("Shop document does not exist for current user. Initializing default.");
                    long freeTrialDays = AuthManager.GlobalAppConfig?.FreeTrialDurationDays ?? 14;

                    shopData = new ShopData
                    {
                        shopName = "", phoneNumber = "", taxId = "", industry = "Chọn Ngành hàng...",
                        eInvoiceProvider = "Chọn Nhà cung cấp...", eInvoiceUser = "", eInvoicePass = "",
                        invoiceSerial = "", invoiceForm = "Chọn Form...", invoiceType = "Chọn Type...",
                        fptAccessToken = "", fptTokenExpiryTime = 0,
                        licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(freeTrialDays)),
                        packageType = "Pro" // MẶC ĐỊNH GÓI PRO CHO THỜI GIAN DÙNG THỬ
                    };
                    await shopDocRef.SetAsync(shopData, SetOptions.MergeAll);
                    StatusPopupManager.Instance.ShowPopup("Đã khởi tạo cài đặt shop với giá trị mặc định.");
                }
                _cachedShopData = shopData;
                ShopSessionData.SetCachedShopSettings(currentUser.UserId, shopData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading shop data from Firebase: {e.Message}");
                StatusPopupManager.Instance.ShowPopup($"Lỗi khi tải dữ liệu shop: {e.Message}");
                _cachedShopData = null; // Đặt null nếu có lỗi
            }
        }
        loadingPanel?.SetActive(false);
        return _cachedShopData;
    }

    // Phương thức mới để hiển thị panel nội dung
    private async void ShowContentPanel(GameObject panelPrefab, string panelName, bool enterEditMode = false)
    {
        if (currentUser == null || _cachedShopData == null)
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng đăng nhập hoặc đợi dữ liệu shop được tải.");
            Debug.LogWarning("ShopSettingManager: User or cached shop data not available when trying to show content panel.");
            return;
        }

        // 1. Đóng panel hiện tại nếu có
        if (_currentActiveContentPanel != null)
        {
            Destroy(_currentActiveContentPanel);
            _currentActiveContentPanel = null;
        }

        // 2. Instantiate panel mới
         if (panelPrefab == null || mainCanvas == null) // <-- THAY ĐỔI ĐIỀU KIỆN để kiểm tra mainCanvas
            {
                Debug.LogError($"ShopSettingManager: Prefab for {panelName} or Main Canvas is not assigned.");
                StatusPopupManager.Instance.ShowPopup("Lỗi: Không tìm thấy giao diện cài đặt.");
                return;
            }

            // Instantiate panel làm con của Canvas chính
            _currentActiveContentPanel = Instantiate(panelPrefab, mainCanvas.transform); // <-- THAY ĐỔI CHA Ở ĐÂY

            // Thiết lập RectTransform để lấp đầy toàn bộ Canvas
            RectTransform panelRect = _currentActiveContentPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;      // Gắn vào góc dưới bên trái của cha
                panelRect.anchorMax = Vector2.one;       // Gắn vào góc trên bên phải của cha
                panelRect.sizeDelta = Vector2.zero;      // Kích thước bằng với cha (Offset về 0)
                panelRect.anchoredPosition = Vector2.zero; // Đặt vị trí tương đối về (0,0)
            }
            _currentActiveContentPanel.transform.localScale = Vector3.one;

        // 3. Truyền dữ liệu và callback cho panel con
        // Dùng switch hoặc if-else để xử lý từng loại panel
        // Mỗi Panel Controller cần có phương thức SetupPanel(ShopData data, Action<ShopData> onSaveCallback, bool editMode = false)

        if (panelPrefab == shopInfoPanelPrefab)
        {
            ShopInfoPanelController shopInfoController = _currentActiveContentPanel.GetComponent<ShopInfoPanelController>();
            if (shopInfoController != null)
            {
                shopInfoController.SetupPanel(_cachedShopData, OnShopInfoSaved, HandleContentPanelClosed, enterEditMode);
            }
            else Debug.LogError("ShopInfoPanelController component not found on instantiated prefab.");
        }
        else if (panelPrefab == eInvoicePanelPrefab)
        {
            EInvoicePanelController eInvoiceController = _currentActiveContentPanel.GetComponent<EInvoicePanelController>();
            if (eInvoiceController != null)
            {
                eInvoiceController.SetupPanel(_cachedShopData, OnEInvoiceSettingsSaved, HandleContentPanelClosed, enterEditMode); // Đã thêm HandleContentPanelClosed
            }
            else Debug.LogError("EInvoicePanelController component not found on instantiated prefab.");
        }
        else if (panelPrefab == packagePanelPrefab)
        {
            PackagePanelController packageController = _currentActiveContentPanel.GetComponent<PackagePanelController>();
            if (packageController != null)
            {
                packageController.SetupPanel(_cachedShopData, OnPackageSettingsSaved, voucherInputField, applyVoucherButton, licenseEndDateText, HandleContentPanelClosed, enterEditMode); // Đã thêm HandleContentPanelClosed
            }
            else Debug.LogError("PackagePanelController component not found on instantiated prefab.");
        }
      else if (panelPrefab == softwareInfoPanelPrefab)
      {
          SoftwareInfoPanelController softwareInfoController = _currentActiveContentPanel.GetComponent<SoftwareInfoPanelController>();
          if (softwareInfoController != null)
          {

              softwareInfoController.SetupPanel(_cachedShopData, enterEditMode); // <-- Đã loại bỏ các callback
          }
          else Debug.LogError("SoftwareInfoPanelController component not found on instantiated prefab.");
      }
        // Thêm các else if cho các panel khác nếu có

        Debug.Log($"ShopSettingManager: Đã hiển thị panel: {panelName}");


    }

    // Các hàm callback khi panel con lưu thành công
    // Các hàm này sẽ cập nhật _cachedShopData và ShopSessionData.CachedShopSettings
    private async void OnShopInfoSaved(ShopData updatedData)
    {
        _cachedShopData = updatedData;
        ShopSessionData.SetCachedShopSettings(currentUser.UserId, updatedData);

        // Thay thế Destroy(_currentActiveContentPanel); _currentActiveContentPanel = null;
        //HandleContentPanelClosed(); // <-- THAY ĐỔI TẠI ĐÂY
        await LoadShopDataFromFirebase(); // Tải lại để đảm bảo đồng bộ hoàn toàn với Firebase
        UpdateLicenseDisplay();
        CheckLicenseAndSetHomepageButtonState();
    }

    private async void OnEInvoiceSettingsSaved(ShopData updatedData)
    {
        _cachedShopData = updatedData;
        ShopSessionData.SetCachedShopSettings(currentUser.UserId, updatedData);

        // Thay thế Destroy(_currentActiveContentPanel); _currentActiveContentPanel = null;
        //HandleContentPanelClosed(); // <-- THAY ĐỔI TẠI ĐÂY
        await LoadShopDataFromFirebase();
        UpdateLicenseDisplay();
        CheckLicenseAndSetHomepageButtonState();
    }

    private async void OnPackageSettingsSaved(ShopData updatedData)
    {
        _cachedShopData = updatedData;
        ShopSessionData.SetCachedShopSettings(currentUser.UserId, updatedData);

        // Thay thế Destroy(_currentActiveContentPanel); _currentActiveContentPanel = null;
       // HandleContentPanelClosed(); // <-- THAY ĐỔI TẠI ĐÂY
        await LoadShopDataFromFirebase();
        UpdateLicenseDisplay();
        CheckLicenseAndSetHomepageButtonState();
    }

    // Hàm riêng để cập nhật hiển thị hạn sử dụng license
    private void UpdateLicenseDisplay()
    {
        if (licenseEndDateText != null && _cachedShopData != null)
        {
            if (_cachedShopData.licenseEndDate != null)
            {
                DateTime endDate = _cachedShopData.licenseEndDate.ToDateTime().ToLocalTime();
                licenseEndDateText.text = $"Hạn sử dụng: {endDate:dd/MM/yyyy HH:mm}";
            }
            else
            {
                licenseEndDateText.text = "Hạn sử dụng: Không xác định";
            }
        }
    }

    // Kiểm tra license để điều khiển nút Homepage
    private void CheckLicenseAndSetHomepageButtonState()
    {
        if (goToHomepageButton != null && _cachedShopData != null && _cachedShopData.licenseEndDate != null)
        {
            if (_cachedShopData.licenseEndDate.ToDateTime() < DateTime.UtcNow)
            {
                goToHomepageButton.gameObject.SetActive(false);
                Debug.Log("License expired. Homepage button hidden.");
            }
            else
            {
                goToHomepageButton.gameObject.SetActive(true);
                Debug.Log("License is valid. Homepage button visible.");
            }
        }
        else if (goToHomepageButton != null)
        {
            goToHomepageButton.gameObject.SetActive(false);
            Debug.Log("No valid license data found. Homepage button hidden.");
        }
    }

    private async void OnApplyVoucherButtonClicked()
    {
        if (currentUser == null || shopDocRef == null || _cachedShopData == null)
        {
            StatusPopupManager.Instance.ShowPopup("Lỗi: Không có thông tin người dùng hoặc dữ liệu shop.");
            return;
        }

        string voucherCode = voucherInputField.text.Trim();
        if (string.IsNullOrEmpty(voucherCode))
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng nhập mã voucher.");
            return;
        }

        loadingPanel?.SetActive(true);
        applyVoucherButton.interactable = false;
        voucherInputField.interactable = false;

        DocumentReference voucherDocRef = db.Collection(VOUCHERS_COLLECTION).Document(voucherCode);

        try
        {
            DocumentSnapshot voucherSnapshot = await voucherDocRef.GetSnapshotAsync();

            if (!voucherSnapshot.Exists)
            {
                StatusPopupManager.Instance.ShowPopup("Voucher không hợp lệ hoặc không tồn tại.");
                return;
            }

            long durationDays = voucherSnapshot.GetValue<long>("durationDays");
            string usageType = voucherSnapshot.GetValue<string>("usageType");
            string voucherPackageName = voucherSnapshot.GetValue<string>("packageName");

            if (usageType == "SINGLE_USE")
            {
                bool isUsed = voucherSnapshot.GetValue<bool>("isUsed");
                if (isUsed)
                {
                    StatusPopupManager.Instance.ShowPopup("Voucher này đã được sử dụng bởi người khác.");
                    return;
                }
            }
            else if (usageType == "MULTIPLE_USE")
            {
                CollectionReference userUsedVouchersRef = db.Collection("shops").Document(currentUser.UserId).Collection("used_vouchers");
                DocumentSnapshot userUsedVoucherDoc = await userUsedVouchersRef.Document(voucherCode).GetSnapshotAsync();

                if (userUsedVoucherDoc.Exists)
                {
                    StatusPopupManager.Instance.ShowPopup("Bạn đã sử dụng voucher này rồi.");
                    return;
                }
            }
            else
            {
                StatusPopupManager.Instance.ShowPopup("Voucher không hợp lệ (kiểu sử dụng không xác định).");
                return;
            }

            if (durationDays <= 0)
            {
                StatusPopupManager.Instance.ShowPopup("Voucher không hợp lệ (thời gian gia hạn không hợp lệ).");
                return;
            }

            DateTime currentEndDate;
            if (_cachedShopData.licenseEndDate != null)
            {
                currentEndDate = _cachedShopData.licenseEndDate.ToDateTime();
            }
            else
            {
                currentEndDate = DateTime.UtcNow;
            }

            if (currentEndDate < DateTime.UtcNow)
            {
                currentEndDate = DateTime.UtcNow;
            }
            DateTime newEndDate = currentEndDate.AddDays(durationDays);

            _cachedShopData.licenseEndDate = Timestamp.FromDateTime(newEndDate);
            if (!string.IsNullOrEmpty(voucherPackageName))
            {
                _cachedShopData.packageType = voucherPackageName;
            }

            WriteBatch batch = db.StartBatch();

            batch.Update(shopDocRef, "licenseEndDate", _cachedShopData.licenseEndDate);
            if (!string.IsNullOrEmpty(voucherPackageName))
            {
                batch.Update(shopDocRef, "packageType", _cachedShopData.packageType);
            }

            if (usageType == "SINGLE_USE")
            {
                Dictionary<string, object> voucherUpdates = new Dictionary<string, object>
                {
                    { "isUsed", true },
                    { "usedByUserId", currentUser.UserId },
                    { "usedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                };
                batch.Update(voucherDocRef, voucherUpdates);
            }
            else if (usageType == "MULTIPLE_USE")
            {
                CollectionReference userUsedVouchersRef = db.Collection("shops").Document(currentUser.UserId).Collection("used_vouchers");
                DocumentReference newUserUsedVoucherDocRef = userUsedVouchersRef.Document(voucherCode);
                batch.Set(newUserUsedVoucherDocRef, new Dictionary<string, object>
                {
                    { "voucherCode", voucherCode },
                    { "appliedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "durationDays", durationDays },
                    { "usageType", usageType },
                    { "packageName", voucherPackageName ?? "" }
                });
            }

            await batch.CommitAsync();

            ShopSessionData.SetCachedShopSettings(currentUser.UserId, _cachedShopData);

            Debug.Log($"Voucher '{voucherCode}' applied successfully. New end date: {newEndDate.ToLocalTime():dd/MM/yyyy HH:mm}");
            StatusPopupManager.Instance.ShowPopup($"Voucher đã được áp dụng. Thời gian sử dụng đã được gia hạn thêm {durationDays} ngày!");
            voucherInputField.text = "";

            UpdateLicenseDisplay(); // Cập nhật hiển thị license
            CheckLicenseAndSetHomepageButtonState(); // Cập nhật trạng thái nút Homepage
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi áp dụng voucher: {e.Message}");
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi áp dụng voucher: {e.Message}");
        }
        finally
        {
            loadingPanel?.SetActive(false);
            applyVoucherButton.interactable = true;
            voucherInputField.interactable = true;
        }
    }

    private void OnGoToHomepageButtonClicked()
    {
        SceneManager.LoadScene("Homepage");
    }

    public void OnLogoutButtonClicked()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.SignOutAndReturnToLogin();
        }
        else
        {
            Debug.LogError("AuthManager instance not found. Cannot log out.");
            StatusPopupManager.Instance.ShowPopup("Lỗi: Không thể đăng xuất. Vui lòng thử lại.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
        }
    }
    private void HandleContentPanelClosed()
    {
        if (_currentActiveContentPanel != null)
        {
            Destroy(_currentActiveContentPanel);
            _currentActiveContentPanel = null;
        }
        Debug.Log("ShopSettingManager: Một panel nội dung đã được đóng.");
    }
}