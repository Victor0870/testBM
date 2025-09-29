// File: Scripts/Customer/CustomerManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore;
using Firebase.Auth;
// ĐÃ XÓA: using SalesDataService; // CS0138
using static ShopSessionData;
using static ShopSettingManager;

public class CustomerManager : MonoBehaviour
{
    [Header("UI References - CustomerManager")]
    public TMP_Text statusText;
    public TMP_InputField searchInputField;
    public Button addNewCustomerButton;
    public Button goToHomepageButton;

    [Header("Customer List Display")]
    public Transform customerListContentParent;
    public GameObject customerItemPrefab; // Cần CustomerUIItem.cs (Mục G)

    [Header("Detail/Edit Panel (Optional - Simple approach)")]
    public GameObject detailEditPanel;
    public TMP_Text editTitleText;
    public TMP_InputField editNameInput;
    public TMP_InputField editPhoneInput;
    public TMP_InputField editAddressInput;
    // Thêm InputField cho các trường khác (taxId, companyName, customerType, idNumber) nếu cần
    public Button saveButton;
    public Button deleteButton;
    public Button cancelButton;

    private List<CustomerData> allCustomers = new List<CustomerData>();
    private CustomerData currentEditingCustomer = null;

    void Start()
    {
        InitializeManager();
        SetupUIListeners();
    }

    void OnDestroy()
    {
        // Hủy đăng ký events để tránh lỗi
        if (SalesDataService.Instance != null)
        {
            SalesDataService.Instance.OnCustomersLoaded -= HandleCustomersLoaded;
        }
    }

    private void InitializeManager()
    {
        if (SalesDataService.Instance == null)
        {
            statusText.text = "Lỗi: SalesDataService chưa được khởi tạo.";
            Debug.LogError("CustomerManager: SalesDataService is null.");
            return;
        }

        // 1. Kiểm tra trạng thái Cloud Sync
        SalesDataService.Instance.CheckCloudSyncStatus();

        // 2. Đăng ký lắng nghe sự kiện tải dữ liệu
        SalesDataService.Instance.OnCustomersLoaded += HandleCustomersLoaded;

        // 3. Yêu cầu tải dữ liệu ban đầu
        LoadCustomers();
    }

    private void SetupUIListeners()
    {
        goToHomepageButton?.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Homepage"));
        addNewCustomerButton?.onClick.AddListener(() => ShowDetailEditPanel(null));

        // Listener cho tìm kiếm
        searchInputField?.onValueChanged.AddListener(OnSearchValueChanged);

        // Listener cho panel chỉnh sửa
        saveButton?.onClick.AddListener(OnSaveButtonClicked);
        deleteButton?.onClick.AddListener(OnDeleteButtonClicked);
        cancelButton?.onClick.AddListener(() => detailEditPanel?.SetActive(false));
    }

    private void LoadCustomers()
    {
        statusText.text = "Đang tải dữ liệu khách hàng...";
        // Yêu cầu Data Service tải dữ liệu từ Cloud (nếu Pro) hoặc Local (nếu Basic)
        SalesDataService.Instance.LoadAndListenForCustomers();
    }

    private void HandleCustomersLoaded(List<CustomerData> customers)
    {
        allCustomers = customers;
        UpdateCustomerUI(allCustomers);
    }

    private void OnSearchValueChanged(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            UpdateCustomerUI(allCustomers);
            return;
        }

        string lowerSearchText = searchText.ToLower();
        // Lọc theo Tên, SĐT, hoặc MST
        var filteredList = allCustomers.Where(c =>
            (c.name != null && c.name.ToLower().Contains(lowerSearchText)) ||
            (c.phone != null && c.phone.Contains(lowerSearchText)) ||
            (c.taxId != null && c.taxId.Contains(lowerSearchText))
        ).ToList();

        UpdateCustomerUI(filteredList);
    }

    private void UpdateCustomerUI(List<CustomerData> customersToDisplay)
    {
        // Xóa các item cũ
        foreach (Transform child in customerListContentParent)
        {
            Destroy(child.gameObject);
        }

        if (customersToDisplay == null || customersToDisplay.Count == 0)
        {
            statusText.text = "Không tìm thấy khách hàng nào.";
            return;
        }

        foreach (var customer in customersToDisplay.OrderBy(c => c.name))
        {
            if (customerItemPrefab != null)
            {
                GameObject itemGO = Instantiate(customerItemPrefab, customerListContentParent);
                CustomerUIItem uiItem = itemGO.GetComponent<CustomerUIItem>();
                if (uiItem != null)
                {
                    uiItem.SetCustomerData(customer, OnEditRequest);
                }
            }
        }

        statusText.text = $"Hiển thị {customersToDisplay.Count} khách hàng (Tổng: {allCustomers.Count}).";
    }

    private void OnEditRequest(CustomerData customerToEdit)
    {
        ShowDetailEditPanel(customerToEdit);
    }

    private void ShowDetailEditPanel(CustomerData customer)
    {
        currentEditingCustomer = customer;
        // Kiểm tra Detail Panel có được gán không
        if (detailEditPanel == null)
        {
            StatusPopupManager.Instance.ShowPopup("Lỗi: Panel chỉnh sửa chưa được gán.");
            return;
        }

        detailEditPanel.SetActive(true);

        if (customer == null)
        {
            // Chế độ Thêm mới
            editTitleText.text = "Thêm Khách hàng Mới";
            editNameInput.text = "";
            editPhoneInput.text = "";
            editAddressInput.text = "";
            deleteButton.gameObject.SetActive(false);
            // TODO: Thiết lập các trường Input/Dropdown khác về giá trị mặc định
        }
        else
        {
            // Chế độ Chỉnh sửa
            editTitleText.text = $"Chỉnh sửa: {customer.name}";
            editNameInput.text = customer.name;
            editPhoneInput.text = customer.phone;
            editAddressInput.text = customer.address;
            deleteButton.gameObject.SetActive(true);
            // TODO: Thiết lập các trường Input/Dropdown khác với dữ liệu của customer
        }
    }

    private async void OnSaveButtonClicked()
    {
        if (string.IsNullOrEmpty(editNameInput.text) || string.IsNullOrEmpty(editPhoneInput.text))
        {
            StatusPopupManager.Instance.ShowPopup("Tên và Số điện thoại không được để trống.");
            return;
        }

        SetInteractablePanel(false);

        // Sử dụng dữ liệu hiện có hoặc tạo mới
        CustomerData dataToSave = currentEditingCustomer ?? new CustomerData();

        // Cập nhật các trường cơ bản từ UI
        dataToSave.name = editNameInput.text.Trim();
        dataToSave.phone = editPhoneInput.text.Trim();
        dataToSave.address = editAddressInput.text.Trim();

        // TODO: Lấy giá trị của các InputField/Dropdown còn lại (taxId, companyName, customerType, idNumber)
        // và gán vào dataToSave.

        try
        {
            // SỬ DỤNG SALESDATASERVICE ĐỂ LƯU (Hàm này tự động xử lý Cloud/Local)
            // Hàm này trả về ID (Cloud/Local) của bản ghi sau khi lưu
            string finalCustomerId = await SalesDataService.Instance.SaveCustomerDataAsync(dataToSave);
            dataToSave.customerId = finalCustomerId;

            StatusPopupManager.Instance.ShowPopup("Đã lưu khách hàng thành công.");
            detailEditPanel.SetActive(false);

            // Nếu Local Only, cần gọi LoadCustomers() lại để refresh UI
            if (!SalesDataService.Instance.IsCloudSyncEnabled)
            {
                LoadCustomers();
            }
        }
        catch (Exception e)
        {
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi lưu khách hàng: {e.Message}");
            Debug.LogError($"Lỗi khi lưu khách hàng: {e.Message}");
        }
        finally
        {
            SetInteractablePanel(true);
        }
    }

    private async void OnDeleteButtonClicked()
    {
        if (currentEditingCustomer == null || string.IsNullOrEmpty(currentEditingCustomer.customerId)) return;

        // Cần xác nhận trước khi xóa (tùy chọn)

        SetInteractablePanel(false);

        try
        {
            // SỬ DỤNG SALESDATASERVICE ĐỂ XÓA (Hàm này tự động xử lý Cloud/Local)
            await SalesDataService.Instance.DeleteCustomerDataAsync(currentEditingCustomer.customerId);

            StatusPopupManager.Instance.ShowPopup("Đã xóa khách hàng thành công.");
            detailEditPanel.SetActive(false);

            if (!SalesDataService.Instance.IsCloudSyncEnabled)
            {
                LoadCustomers();
            }
        }
        catch (Exception e)
        {
            StatusPopupManager.Instance.ShowPopup($"Lỗi khi xóa khách hàng: {e.Message}");
            Debug.LogError($"Lỗi khi xóa khách hàng: {e.Message}");
        }
        finally
        {
            SetInteractablePanel(true);
        }
    }

    private void SetInteractablePanel(bool interactable)
    {
        if (detailEditPanel != null)
        {
            CanvasGroup canvasGroup = detailEditPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = detailEditPanel.AddComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }
}