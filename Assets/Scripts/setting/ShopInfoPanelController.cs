// File: ShopInfoPanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using static ShopSettingManager; // Để sử dụng ShopData
// Thêm StatusPopupManager nếu cần thông báo lỗi trực tiếp từ panel

public class ShopInfoPanelController : MonoBehaviour
{
    [Header("Shop Info UI")]
    public TMP_InputField shopNameInput;
    public TMP_InputField phoneNumberInput;
    public TMP_InputField taxIdInput;
    public TMP_Dropdown industryDropdown;

    public Button editButton;
    public Button saveButton;
    public Button cancelEditButton;
    public Button closePanelButton;

    private ShopData _originalShopData; // Lưu trữ dữ liệu gốc để so sánh khi hủy
    private ShopData _currentEditableData; // Dữ liệu đang được chỉnh sửa
    private Action<ShopData> _onSaveCallback; // Callback để báo cho ShopSettingManager biết đã lưu

    private FirebaseFirestore _db;
    private Firebase.Auth.FirebaseUser _currentUser;
    private DocumentReference _shopDocRef;

    private bool _isInEditMode = false;
    private bool _listenersInitialized = false; // Cờ để đảm bảo listeners chỉ gán một lần
    private Action _onClosePanelCallback;

    void Awake()
    {
        _db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null); // Gọi ban đầu để lấy user

        PopulateIndustryDropdown(); // Hàm này sẽ được gọi khi panel được tạo
    }

    void OnDestroy()
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
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
                Debug.LogWarning("ShopInfoPanelController: User logged out, shopDocRef cleared.");
            }
        }
    }

    private void PopulateIndustryDropdown()
    {
        if (industryDropdown == null) return;
        industryDropdown.ClearOptions();
        List<string> options = new List<string> { "Chọn Ngành hàng...", "Y tế", "Thời trang", "Tạp hóa", "Điện tử", "Khác" };
        industryDropdown.AddOptions(options);
    }

    // SetupPanel được gọi từ ShopSettingManager
    public void SetupPanel(ShopData data, Action<ShopData> onSaveCallback,Action onClosePanelCallback,bool editMode = false)
    {
        _originalShopData = data;
        _onClosePanelCallback = onClosePanelCallback;
        // Tạo bản sao để chỉnh sửa, tránh thay đổi trực tiếp _originalShopData
        _currentEditableData = new ShopData
        {
            shopName = data.shopName,
            phoneNumber = data.phoneNumber,
            taxId = data.taxId,
            industry = data.industry,
            // Đảm bảo sao chép tất cả các thuộc tính quan trọng khác từ ShopData
            eInvoiceProvider = data.eInvoiceProvider,
            eInvoiceUser = data.eInvoiceUser,
            eInvoicePass = data.eInvoicePass,
            invoiceSerial = data.invoiceSerial,
            invoiceForm = data.invoiceForm,
            invoiceType = data.invoiceType,
            fptAccessToken = data.fptAccessToken,
            fptTokenExpiryTime = data.fptTokenExpiryTime,
            licenseEndDate = data.licenseEndDate,
            packageType = data.packageType
        };
        _onSaveCallback = onSaveCallback;

        DisplayData(_originalShopData); // Hiển thị dữ liệu gốc
        SetEditMode(editMode); // Đặt chế độ chỉnh sửa theo yêu cầu từ bên ngoài

        if (!_listenersInitialized)
        {
            editButton?.onClick.AddListener(OnEditButtonClicked);
            saveButton?.onClick.AddListener(OnSaveButtonClicked);
            cancelEditButton?.onClick.AddListener(OnCancelEditButtonClicked);
            closePanelButton?.onClick.AddListener(OnClosePanelButtonClicked);
            _listenersInitialized = true;
        }
    }

    private void DisplayData(ShopData data)
    {
        if (shopNameInput != null) shopNameInput.text = data.shopName ?? "";
        if (phoneNumberInput != null) phoneNumberInput.text = data.phoneNumber ?? "";
        if (taxIdInput != null) taxIdInput.text = data.taxId ?? "";

        if (industryDropdown != null)
        {
            int industryIndex = industryDropdown.options.FindIndex(option => option.text == data.industry);
            industryDropdown.value = industryIndex != -1 ? industryIndex : 0;
        }
    }

    private void SetInteractableInputs(bool interactable)
    {
        if (shopNameInput != null) shopNameInput.interactable = interactable;
        if (phoneNumberInput != null) phoneNumberInput.interactable = interactable;
        if (taxIdInput != null) taxIdInput.interactable = interactable;
        if (industryDropdown != null) industryDropdown.interactable = interactable;

        // Đặt màu nền cho InputField
        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
        Color editableColor = Color.white;

        SetInputFieldColor(shopNameInput, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(phoneNumberInput, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(taxIdInput, interactable ? editableColor : readOnlyColor);
    }

    private void SetInputFieldColor(TMP_InputField inputField, Color color)
    {
        if (inputField != null && inputField.targetGraphic != null)
        {
            inputField.targetGraphic.color = color;
        }
    }

    private void SetEditMode(bool editMode)
    {
        _isInEditMode = editMode;
        if (editButton != null) editButton.gameObject.SetActive(!editMode);
        if (saveButton != null) saveButton.gameObject.SetActive(editMode);
        if (cancelEditButton != null) cancelEditButton.gameObject.SetActive(editMode);
        SetInteractableInputs(editMode);
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

        // Cập nhật dữ liệu từ UI vào _currentEditableData
        _currentEditableData.shopName = shopNameInput.text.Trim();
        _currentEditableData.phoneNumber = phoneNumberInput.text.Trim();
        _currentEditableData.taxId = taxIdInput.text.Trim();
        _currentEditableData.industry = industryDropdown.options[industryDropdown.value].text;

        // Kiểm tra validation
        if (string.IsNullOrEmpty(_currentEditableData.shopName) ||
            string.IsNullOrEmpty(_currentEditableData.phoneNumber) ||
            string.IsNullOrEmpty(_currentEditableData.taxId) ||
            _currentEditableData.industry == "Chọn Ngành hàng...")
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng điền đầy đủ các thông tin bắt buộc.");
            return;
        }

        SetInteractableInputs(false); // Vô hiệu hóa input trong khi lưu
        if (saveButton != null) saveButton.interactable = false;
        if (cancelEditButton != null) cancelEditButton.interactable = false;

        try
        {
            await _shopDocRef.SetAsync(_currentEditableData, SetOptions.MergeAll);
            Debug.Log("Thông tin cửa hàng đã được lưu thành công.");
            SetEditMode(false);

            _originalShopData = _currentEditableData; // Cập nhật dữ liệu gốc
            _onSaveCallback?.Invoke(_currentEditableData); // Gọi callback về ShopSettingManager để cập nhật cache và hiển thị
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi lưu thông tin cửa hàng: {e.Message}");
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi lưu thông tin cửa hàng: {e.Message}");
            SetInteractableInputs(true); // Kích hoạt lại nếu lỗi
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