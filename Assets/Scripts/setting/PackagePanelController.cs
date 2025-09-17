// File: PackagePanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using static ShopSettingManager; // Để sử dụng ShopData
using static ShopSessionData; // Truy cập AppPackageConfig

public class PackagePanelController : MonoBehaviour
{
    [Header("Subscription Package UI")]
    public Toggle basicPackageToggle;
    public Toggle advancedPackageToggle;
    public Toggle proPackageToggle;
    public TMP_Text currentPackageConfirmationText; // Text xác nhận gói đang lựa chọn

    // Các trường voucher và license sẽ được truyền từ ShopSettingManager
    private TMP_InputField _voucherInputField;
    private Button _applyVoucherButton;
    private TMP_Text _licenseEndDateText; // Chỉ để hiển thị, không edit ở đây

    public Button editButton; // Nút Edit cho phần này
    public Button saveButton; // Nút Save cho phần này
    public Button cancelEditButton; // Nút Cancel cho phần này
    public Button closePanelButton;

    private ShopData _originalShopData;
    private ShopData _currentEditableData;
    private Action<ShopData> _onSaveCallback;
    private Action _onClosePanelCallback;

    private FirebaseFirestore _db;
    private Firebase.Auth.FirebaseUser _currentUser;
    private DocumentReference _shopDocRef;

    private bool _isInEditMode = false;
    private bool _listenersInitialized = false;

    void Awake()
    {
        _db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    void OnDestroy()
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
        // Gỡ listener của toggle nếu cần thiết để tránh lỗi khi đối tượng bị hủy
        if (basicPackageToggle != null) basicPackageToggle.onValueChanged.RemoveAllListeners();
        if (advancedPackageToggle != null) advancedPackageToggle.onValueChanged.RemoveAllListeners();
        if (proPackageToggle != null) proPackageToggle.onValueChanged.RemoveAllListeners();
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        Firebase.Auth.FirebaseUser newUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != _currentUser)
        {
            _currentUser = newUser;
            if (_currentUser != null)
            {
                _shopDocRef = _db.Collection("shops").Document(_currentUser.UserId);
            }
            else
            {
                _shopDocRef = null;
                Debug.LogWarning("PackagePanelController: User logged out, shopDocRef cleared.");
            }
        }
    }

    public void SetupPanel(ShopData data, Action<ShopData> onSaveCallback, TMP_InputField voucherInput, Button applyVoucherBtn, TMP_Text licenseText, Action onClosePanelCallback, bool editMode = false)
    {
        _onClosePanelCallback = onClosePanelCallback;
        _originalShopData = data;
        _currentEditableData = new ShopData
        {
            shopName = data.shopName, phoneNumber = data.phoneNumber, taxId = data.taxId, industry = data.industry,
            eInvoiceProvider = data.eInvoiceProvider, eInvoiceUser = data.eInvoiceUser, eInvoicePass = data.eInvoicePass,
            invoiceSerial = data.invoiceSerial, invoiceForm = data.invoiceForm, invoiceType = data.invoiceType,
            fptAccessToken = data.fptAccessToken, fptTokenExpiryTime = data.fptTokenExpiryTime,
            licenseEndDate = data.licenseEndDate, packageType = data.packageType
        };
        _onSaveCallback = onSaveCallback;

        _voucherInputField = voucherInput;
        _applyVoucherButton = applyVoucherBtn;
        _licenseEndDateText = licenseText;

        DisplayData(_originalShopData);
        SetEditMode(editMode); // Đặt chế độ chỉnh sửa theo yêu cầu từ bên ngoài

        if (!_listenersInitialized)
        {
            // Listener cho Toggle
            if (basicPackageToggle != null) basicPackageToggle.onValueChanged.AddListener(delegate { OnPackageToggleChanged(basicPackageToggle, "Basic"); });
            if (advancedPackageToggle != null) advancedPackageToggle.onValueChanged.AddListener(delegate { OnPackageToggleChanged(advancedPackageToggle, "Advanced"); });
            if (proPackageToggle != null) proPackageToggle.onValueChanged.AddListener(delegate { OnPackageToggleChanged(proPackageToggle, "Pro"); });

            editButton?.onClick.AddListener(OnEditButtonClicked);
            saveButton?.onClick.AddListener(OnSaveButtonClicked);
            cancelEditButton?.onClick.AddListener(OnCancelEditButtonClicked);
            closePanelButton?.onClick.AddListener(OnClosePanelButtonClicked);
            _listenersInitialized = true;
        }
    }

    private void DisplayData(ShopData data)
    {
        if (basicPackageToggle != null) basicPackageToggle.isOn = (data.packageType == "Basic");
        if (advancedPackageToggle != null) advancedPackageToggle.isOn = (data.packageType == "Advanced");
        if (proPackageToggle != null) proPackageToggle.isOn = (data.packageType == "Pro");

        // Đảm bảo có một gói được chọn nếu dữ liệu từ Firestore rỗng hoặc không khớp
        if (!basicPackageToggle.isOn && !advancedPackageToggle.isOn && !proPackageToggle.isOn)
        {
            if (basicPackageToggle != null) basicPackageToggle.isOn = true; // Mặc định chọn Basic
            if (currentPackageConfirmationText != null) currentPackageConfirmationText.text = "Gói đang chọn: Basic";
            _currentEditableData.packageType = "Basic"; // Cập nhật lại trong shopData để đồng bộ
        }
        else // Cập nhật text xác nhận dựa trên gói đang ON
        {
            if (basicPackageToggle.isOn) currentPackageConfirmationText.text = "Gói đang chọn: Basic";
            else if (advancedPackageToggle.isOn) currentPackageConfirmationText.text = "Gói đang chọn: Advanced";
            else if (proPackageToggle.isOn) currentPackageConfirmationText.text = "Gói đang chọn: Pro";
        }
    }

    private void OnPackageToggleChanged(Toggle changedToggle, string packageName)
    {
        if (changedToggle.isOn)
        {
            // Đảm bảo chỉ Toggle này là TRUE, các Toggle khác là FALSE
            if (basicPackageToggle != null && basicPackageToggle != changedToggle) basicPackageToggle.isOn = false;
            if (advancedPackageToggle != null && advancedPackageToggle != changedToggle) advancedPackageToggle.isOn = false;
            if (proPackageToggle != null && proPackageToggle != changedToggle) proPackageToggle.isOn = false;

            // Cập nhật Text xác nhận và dữ liệu chỉnh sửa
            if (currentPackageConfirmationText != null)
            {
                currentPackageConfirmationText.text = $"Gói đang chọn: {packageName}";
            }
            _currentEditableData.packageType = packageName;
            Debug.Log($"Đã chọn gói: {packageName}");
        }
        else
        {
            // Logic này để đảm bảo LUÔN CÓ ÍT NHẤT MỘT gói được chọn.
            if (basicPackageToggle != null && !basicPackageToggle.isOn &&
                advancedPackageToggle != null && !advancedPackageToggle.isOn &&
                proPackageToggle != null && !proPackageToggle.isOn)
            {
                if (changedToggle != null) changedToggle.isOn = true; // Đảm bảo luôn có 1 gói ON
                if (currentPackageConfirmationText != null) currentPackageConfirmationText.text = $"Gói đang chọn: {packageName}";
                Debug.Log("Không thể bỏ chọn gói duy nhất. Gói này vẫn được chọn.");
            }
        }
    }

    private void SetInteractableInputs(bool interactable)
    {
        if (basicPackageToggle != null) basicPackageToggle.interactable = interactable;
        if (advancedPackageToggle != null) advancedPackageToggle.interactable = interactable;
        if (proPackageToggle != null) proPackageToggle.interactable = interactable;
    }

    private void SetEditMode(bool editMode)
    {
        _isInEditMode = editMode;
        if (editButton != null) editButton.gameObject.SetActive(!editMode);
        if (saveButton != null) saveButton.gameObject.SetActive(editMode);
        if (cancelEditButton != null) cancelEditButton.gameObject.SetActive(editMode);
        SetInteractableInputs(editMode); // Điều khiển toggle

        // Voucher input/button được điều khiển bởi ShopSettingManager.OnApplyVoucherButtonClicked
        // Nhưng cần đảm bảo chúng không bị ẩn/hiện bởi panel này.
        // Cần đảm bảo voucherInputField và applyVoucherButton luôn interactable
        // nếu đang trong chế độ edit (hoặc tùy thuộc vào luồng của bạn)
    }

    private void OnEditButtonClicked()
    {
        SetEditMode(true);

    }

    private async void OnSaveButtonClicked()
    {
        if (_currentUser == null || _shopDocRef == null || _currentEditableData == null)
        {
            StatusPopupManager.Instance.ShowPopup("Lỗi: Không có thông tin người dùng hoặc dữ liệu.");
            return;
        }

        // Cập nhật packageType đã chọn từ Toggle vào _currentEditableData
        // (đã được cập nhật trong OnPackageToggleChanged)

        SetInteractableInputs(false);
        if (saveButton != null) saveButton.interactable = false;
        if (cancelEditButton != null) cancelEditButton.interactable = false;

        try
        {
            // Chỉ cập nhật trường packageType lên Firestore
            await _shopDocRef.UpdateAsync("packageType", _currentEditableData.packageType);
            Debug.Log("Cài đặt gói sử dụng đã được lưu thành công.");


            _originalShopData = _currentEditableData; // Cập nhật dữ liệu gốc
            _onSaveCallback?.Invoke(_currentEditableData); // Gọi callback về ShopSettingManager
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi lưu cài đặt gói sử dụng: {e.Message}");
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi lưu cài đặt gói sử dụng: {e.Message}");
            SetInteractableInputs(true);
            if (saveButton != null) saveButton.interactable = true;
            if (cancelEditButton != null) cancelEditButton.interactable = true;
        }
    }

    private void OnCancelEditButtonClicked()
    {
        DisplayData(_originalShopData); // Hiển thị lại dữ liệu gốc
        SetEditMode(false);

        _onSaveCallback?.Invoke(_originalShopData);
    }
    private void OnClosePanelButtonClicked()
        {
            _onClosePanelCallback?.Invoke();
        }
}