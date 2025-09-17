// File: EInvoicePanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using static ShopSettingManager; // Để sử dụng ShopData

public class EInvoicePanelController : MonoBehaviour
{
    [Header("E-Invoice Settings UI")]
    public TMP_Dropdown eInvoiceProviderDropdown;
    public TMP_InputField eInvoiceUserInput;
    public TMP_InputField eInvoicePassInput; // Cẩn thận khi lưu mật khẩu, không nên lưu plaintext
    public TMP_InputField invoiceSerialInput;
    public TMP_Dropdown invoiceFormDropdown;
    public TMP_Dropdown invoiceTypeDropdown;

    public Button editButton;
    public Button saveButton;
    public Button cancelEditButton;
    public Button closePanelButton;

    private ShopData _originalShopData;
    private ShopData _currentEditableData;
    private Action<ShopData> _onSaveCallback;

    private FirebaseFirestore _db;
    private Firebase.Auth.FirebaseUser _currentUser;
    private DocumentReference _shopDocRef;

    private bool _isInEditMode = false;
    private bool _listenersInitialized = false;
    private Action _onClosePanelCallback;

    void Awake()
    {
        _db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);

        PopulateDropdowns();
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
                Debug.LogWarning("EInvoicePanelController: User logged out, shopDocRef cleared.");
            }
        }
    }

    private void PopulateDropdowns()
    {
        if (eInvoiceProviderDropdown != null)
        {
            eInvoiceProviderDropdown.ClearOptions();
            eInvoiceProviderDropdown.AddOptions(new List<string> { "Chọn Nhà cung cấp...", "FPT", "Viettel", "VNPT", "Khác" });
        }
        if (invoiceFormDropdown != null)
        {
            invoiceFormDropdown.ClearOptions();
            invoiceFormDropdown.AddOptions(new List<string> { "Chọn Form...", "1", "2", "5" });
        }
        if (invoiceTypeDropdown != null)
        {
            invoiceTypeDropdown.ClearOptions();
            invoiceTypeDropdown.AddOptions(new List<string> { "Chọn Type...", "01/MTT", "02/MTT", "05/MTT", "08/MTT" });
        }
    }

    public void SetupPanel(ShopData data, Action<ShopData> onSaveCallback, Action onClosePanelCallback, bool editMode = false)
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

        DisplayData(_originalShopData);
        SetEditMode(editMode);

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
        if (eInvoiceProviderDropdown != null)
        {
            int providerIndex = eInvoiceProviderDropdown.options.FindIndex(option => option.text == data.eInvoiceProvider);
            eInvoiceProviderDropdown.value = providerIndex != -1 ? providerIndex : 0;
        }
        if (eInvoiceUserInput != null) eInvoiceUserInput.text = data.eInvoiceUser ?? "";
        if (eInvoicePassInput != null) eInvoicePassInput.text = data.eInvoicePass ?? "";
        if (invoiceSerialInput != null) invoiceSerialInput.text = data.invoiceSerial ?? "";
        if (invoiceFormDropdown != null)
        {
            int formIndex = invoiceFormDropdown.options.FindIndex(option => option.text == data.invoiceForm);
            invoiceFormDropdown.value = formIndex != -1 ? formIndex : 0;
        }
        if (invoiceTypeDropdown != null)
        {
            int typeIndex = invoiceTypeDropdown.options.FindIndex(option => option.text == data.invoiceType);
            invoiceTypeDropdown.value = typeIndex != -1 ? typeIndex : 0;
        }
    }

    private void SetInteractableInputs(bool interactable)
    {
        if (eInvoiceProviderDropdown != null) eInvoiceProviderDropdown.interactable = interactable;
        if (eInvoiceUserInput != null) eInvoiceUserInput.interactable = interactable;
        if (eInvoicePassInput != null) eInvoicePassInput.interactable = interactable;
        if (invoiceSerialInput != null) invoiceSerialInput.interactable = interactable;
        if (invoiceFormDropdown != null) invoiceFormDropdown.interactable = interactable;
        if (invoiceTypeDropdown != null) invoiceTypeDropdown.interactable = interactable;

        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
        Color editableColor = Color.white;

        SetInputFieldColor(eInvoiceUserInput, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(eInvoicePassInput, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(invoiceSerialInput, interactable ? editableColor : readOnlyColor);
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

        _currentEditableData.eInvoiceProvider = eInvoiceProviderDropdown.options[eInvoiceProviderDropdown.value].text;
        _currentEditableData.eInvoiceUser = eInvoiceUserInput.text.Trim();
        _currentEditableData.eInvoicePass = eInvoicePassInput.text.Trim(); // Cần mã hóa thực tế
        _currentEditableData.invoiceSerial = invoiceSerialInput.text.Trim();
        _currentEditableData.invoiceForm = invoiceFormDropdown.options[invoiceFormDropdown.value].text;
        _currentEditableData.invoiceType = invoiceTypeDropdown.options[invoiceTypeDropdown.value].text;

        if (_currentEditableData.eInvoiceProvider != "FPT" && _currentEditableData.eInvoiceProvider != "Chọn Nhà cung cấp...")
        {
            StatusPopupManager.Instance.ShowPopup($"Hiện tại chỉ hỗ trợ tích hợp với nhà cung cấp hóa đơn FPT. Vui lòng chọn 'FPT' hoặc 'Chọn Nhà cung cấp...'.");
            return;
        }
        if (_currentEditableData.invoiceForm == "Chọn Form..." || _currentEditableData.invoiceType == "Chọn Type...")
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng chọn Form và Type hóa đơn hợp lệ.");
            return;
        }
        // Thêm validation cho eInvoiceUser/Pass nếu nhà cung cấp là FPT

        SetInteractableInputs(false);
        if (saveButton != null) saveButton.interactable = false;
        if (cancelEditButton != null) cancelEditButton.interactable = false;

        try
        {
            await _shopDocRef.SetAsync(_currentEditableData, SetOptions.MergeAll);
            Debug.Log("Cài đặt hóa đơn điện tử đã được lưu thành công.");


            _originalShopData = _currentEditableData;
            _onSaveCallback?.Invoke(_currentEditableData);
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi lưu cài đặt hóa đơn điện tử: {e.Message}");
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi lưu cài đặt hóa đơn điện tử: {e.Message}");
            SetInteractableInputs(true);
            if (saveButton != null) saveButton.interactable = true;
            if (cancelEditButton != null) cancelEditButton.interactable = true;
        }
    }

    private void OnCancelEditButtonClicked()
    {
        DisplayData(_originalShopData);
        SetEditMode(false);

        _onSaveCallback?.Invoke(_originalShopData);
    }
     private void OnClosePanelButtonClicked()
        {
            _onClosePanelCallback?.Invoke(); //  P
        }
}