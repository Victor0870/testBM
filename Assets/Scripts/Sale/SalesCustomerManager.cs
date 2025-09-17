// File: SalesCustomerManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth; // Cần cho FirebaseUser
using System;
using System.Linq; // Cần cho FirstOrDefault
using System.Collections.Generic;

// Đảm bảo các using cần thiết từ SalesManager được đưa vào đây
using static ShopSessionData; // Để truy cập ShopSessionData.CachedShopSettings, GlobalAppConfig
using static ShopSettingManager; // Cần cho ShopData class, nếu bạn muốn dùng các enum/const từ đây.
                                // Hoặc chỉ cần ShopSettingManager.ShopData nếu đó là cần thiết.


public class SalesCustomerManager : MonoBehaviour
{
    [Header("Customer Info UI - SalesCustomerManager")]
    public TMP_Dropdown customerTypeDropdown;
    public TMP_InputField customerPhoneInputField;
    public TMP_InputField customerNameInputField;
    public TMP_InputField customerAddressInputField;
    public TMP_InputField customerIdNumberInputField;

    public TextMeshProUGUI customerCompanyNameLabel;
    public TMP_InputField customerCompanyNameInputField;

    public TextMeshProUGUI customerTaxIdLabel;
    public TMP_InputField customerTaxIdInputField;

    public TMP_Text customerLookupStatusText;
    public Button clearCustomerInfoButton;

    // Tham chiếu đến StatusPopupManager (được truyền từ SalesManager chính)
    private StatusPopupManager _statusPopupManager;
    // Tham chiếu đến Firebase (được truyền từ SalesManager chính)
    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userCustomersCollection;

    // Dữ liệu khách hàng hiện tại
    private CustomerData _currentCustomer;
    public CustomerData CurrentCustomer => _currentCustomer; // Public getter cho SalesManager chính

    // Event để thông báo khi thông tin khách hàng được cập nhật (ví dụ: sau tra cứu)
    public event Action OnCustomerInfoUpdated;


    // Phương thức khởi tạo, được gọi từ SalesManager chính
    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser, CollectionReference userCustomersCollection, StatusPopupManager statusPopupManager)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userCustomersCollection = userCustomersCollection;
        _statusPopupManager = statusPopupManager;

        // Gán listener cho các sự kiện UI của Customer Manager
        if (customerPhoneInputField != null) customerPhoneInputField.onEndEdit.AddListener(OnCustomerPhoneEndEdit);
        if (clearCustomerInfoButton != null) clearCustomerInfoButton.onClick.AddListener(ClearCustomerInfo);
        if (customerTypeDropdown != null) customerTypeDropdown.onValueChanged.AddListener(OnCustomerTypeChanged);

        // Thiết lập trạng thái UI ban đầu
        SetupInitialUIState();
    }

    private void SetupInitialUIState()
    {
        // Khởi tạo Dropdown loại khách hàng
        if (customerTypeDropdown != null)
        {
            customerTypeDropdown.ClearOptions();
            customerTypeDropdown.AddOptions(new List<string> { "Khách lẻ", "Công ty" });
            customerTypeDropdown.value = 0; // Mặc định là "Khách lẻ"
        }
        // Gọi OnCustomerTypeChanged để thiết lập hiển thị/ẩn các trường liên quan
        OnCustomerTypeChanged(customerTypeDropdown.value); //

        // Ban đầu, các input fields không tương tác, ngoại trừ SĐT
        SetAllCustomerInputFieldsInteractable(false);
        if (customerPhoneInputField != null) customerPhoneInputField.interactable = true;
    }


    private void OnCustomerTypeChanged(int index)
    {
        if (customerTypeDropdown == null) return;

        string selectedType = customerTypeDropdown.options[index].text;
        bool isIndividual = (selectedType == "Khách lẻ");

        // Điều khiển hiển thị/tương tác của các trường dành riêng cho "Công ty"
        if (customerCompanyNameInputField != null)
        {
            customerCompanyNameInputField.gameObject.SetActive(!isIndividual);
            customerCompanyNameInputField.interactable = !isIndividual;
            if (isIndividual) customerCompanyNameInputField.text = "";
        }
        if (customerCompanyNameLabel != null)
        {
            customerCompanyNameLabel.gameObject.SetActive(!isIndividual);
        }

        if (customerTaxIdInputField != null)
        {
            customerTaxIdInputField.gameObject.SetActive(!isIndividual);
            customerTaxIdInputField.interactable = !isIndividual;
            if (isIndividual) customerTaxIdInputField.text = "";
        }
        if (customerTaxIdLabel != null)
        {
            customerTaxIdLabel.gameObject.SetActive(!isIndividual);
        }

        // Trường CCCD: LUÔN HIỂN THỊ
        if (customerIdNumberInputField != null)
        {
            customerIdNumberInputField.gameObject.SetActive(true);
            if (!isIndividual) customerIdNumberInputField.text = ""; // Xóa giá trị nếu chuyển về công ty
        }

        // Cập nhật trạng thái tương tác chung cho các fields nếu cần
        // SetAllCustomerInputFieldsInteractable(true); // Tùy chọn: Kích hoạt lại tất cả fields khi thay đổi loại KH
    }

    private void OnCustomerPhoneEndEdit(string phone)
    {
        OnLookupCustomerButtonClicked();
    }

    public async void OnLookupCustomerButtonClicked()
    {
        string phone = customerPhoneInputField.text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            _statusPopupManager.ShowPopup("Vui lòng nhập số điện thoại khách hàng.");
            customerPhoneInputField.interactable = true; // SĐT vẫn cho phép nhập
            if (customerLookupStatusText != null) customerLookupStatusText.text = "";
            OnCustomerTypeChanged(customerTypeDropdown.value); // Reset UI theo loại KH
            SetAllCustomerInputFieldsInteractable(true); // Mở lại tương tác cho các trường khác để nhập
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            _statusPopupManager.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            SetAllCustomerInputFieldsInteractable(false);
            customerPhoneInputField.interactable = true;
            if (customerLookupStatusText != null) customerLookupStatusText.text = "";
            return;
        }

        if (_currentUser == null || _userCustomersCollection == null)
        {
            _statusPopupManager.ShowPopup("Lỗi: Thông tin người dùng hoặc collection khách hàng chưa được thiết lập.");
            Debug.LogError("SalesCustomerManager: Firebase User or Collection not set.");
            SetAllCustomerInputFieldsInteractable(true);
            customerPhoneInputField.interactable = true;
            return;
        }


        if (customerLookupStatusText != null) customerLookupStatusText.text = "Đang tra cứu khách hàng...";
        SetAllCustomerInputFieldsInteractable(false); // Vô hiệu hóa tất cả fields khi đang tra cứu
        customerPhoneInputField.interactable = false; // Vô hiệu hóa SĐT khi đang tra cứu

        try
        {
            QuerySnapshot querySnapshot = await _userCustomersCollection.WhereEqualTo("phone", phone).Limit(1).GetSnapshotAsync();

            if (querySnapshot.Count > 0)
            {
                DocumentSnapshot doc = querySnapshot.Documents.FirstOrDefault();
                _currentCustomer = doc.ConvertTo<CustomerData>();
                _currentCustomer.customerId = doc.Id;

                // Cập nhật UI từ dữ liệu khách hàng tìm thấy
                customerNameInputField.text = _currentCustomer.name;
                customerAddressInputField.text = _currentCustomer.address;
                customerIdNumberInputField.text = _currentCustomer.idNumber;
                customerTaxIdInputField.text = _currentCustomer.taxId;
                customerCompanyNameInputField.text = _currentCustomer.companyName;

                // Cập nhật Dropdown loại khách hàng và UI theo dữ liệu
                if (!string.IsNullOrEmpty(_currentCustomer.customerType))
                {
                    customerTypeDropdown.value = _currentCustomer.customerType == "Công ty" ? 1 : 0;
                }
                else
                {
                    customerTypeDropdown.value = 0; // Mặc định Khách lẻ nếu thiếu
                }
                OnCustomerTypeChanged(customerTypeDropdown.value); // Cập nhật hiển thị/ẩn fields

                SetAllCustomerInputFieldsInteractable(false); // Vô hiệu hóa để xem (trừ SĐT)
                customerPhoneInputField.interactable = true; // SĐT vẫn cho phép sửa để tra cứu lại
                
                if (customerLookupStatusText != null) customerLookupStatusText.text = $"Đã tìm thấy khách hàng: {_currentCustomer.name}.";
            }
            else
            {
                // Reset dữ liệu và UI nếu không tìm thấy
                _currentCustomer = new CustomerData { phone = phone }; // Dùng constructor đã có để set phone
                customerNameInputField.text = "";
                customerAddressInputField.text = "";
                customerIdNumberInputField.text = "";
                customerTaxIdInputField.text = "";
                customerCompanyNameInputField.text = "";

                customerTypeDropdown.value = 0; // Mặc định Khách lẻ
                OnCustomerTypeChanged(0); // Cập nhật UI về trạng thái Khách lẻ và kích hoạt fields

                SetAllCustomerInputFieldsInteractable(true); // Kích hoạt tất cả fields để nhập mới
                customerPhoneInputField.interactable = true; // SĐT vẫn tương tác
                
                if (customerLookupStatusText != null) customerLookupStatusText.text = "Không tìm thấy khách hàng. Vui lòng nhập thông tin mới.";
            }
            OnCustomerInfoUpdated?.Invoke(); // Kích hoạt event để SalesManager biết
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi tra cứu khách hàng: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi tra cứu khách hàng. Vui lòng kiểm tra mạng của bạn.";
            }
            _statusPopupManager.ShowPopup(errorMessage);
            Debug.LogError($"Lỗi khi tra cứu khách hàng: {e.Message}");
            SetAllCustomerInputFieldsInteractable(true);
            customerPhoneInputField.interactable = true;
            if (customerLookupStatusText != null) customerLookupStatusText.text = "Lỗi tra cứu.";
        }
        finally
        {
            customerPhoneInputField.interactable = true;
        }
    }

    // Phương thức điều khiển khả năng tương tác của TẤT CẢ các InputField khách hàng
    public void SetAllCustomerInputFieldsInteractable(bool interactable)
    {
        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
        Color editableColor = Color.white;

        // Các trường chung luôn hiển thị
        if (customerTypeDropdown != null) customerTypeDropdown.interactable = interactable;
        if (customerNameInputField != null) customerNameInputField.interactable = interactable;
        if (customerAddressInputField != null) customerAddressInputField.interactable = interactable;
        if (customerIdNumberInputField != null) customerIdNumberInputField.interactable = interactable; // CCCD luôn tương tác

        SetInputFieldColor(customerNameInputField, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(customerAddressInputField, interactable ? editableColor : readOnlyColor);
        SetInputFieldColor(customerIdNumberInputField, interactable ? editableColor : readOnlyColor); // CCCD màu

        // Các trường phụ thuộc vào loại khách hàng (chỉ set interactable và màu nếu chúng đang active)
        bool isIndividual = (customerTypeDropdown != null && customerTypeDropdown.value == 0);

        if (customerTaxIdInputField != null && customerTaxIdInputField.gameObject.activeSelf)
        {
            customerTaxIdInputField.interactable = interactable && !isIndividual;
            SetInputFieldColor(customerTaxIdInputField, interactable && !isIndividual ? editableColor : readOnlyColor);
        }

        if (customerCompanyNameInputField != null && customerCompanyNameInputField.gameObject.activeSelf)
        {
            customerCompanyNameInputField.interactable = interactable && !isIndividual;
            SetInputFieldColor(customerCompanyNameInputField, interactable && !isIndividual ? editableColor : readOnlyColor);
        }
        // Riêng số điện thoại luôn được giữ interactable nếu không đang tra cứu
    }

    private void SetInputFieldColor(TMP_InputField inputField, Color color)
    {
        if (inputField != null && inputField.targetGraphic != null)
        {
            inputField.targetGraphic.color = color;
        }
    }

    public void ClearCustomerInfo()
    {
        _currentCustomer = null;
        customerPhoneInputField.text = "";
        customerNameInputField.text = "";
        customerAddressInputField.text = "";
        customerIdNumberInputField.text = "";
        customerTaxIdInputField.text = "";
        customerCompanyNameInputField.text = "";

        if (customerTypeDropdown != null)
        {
            customerTypeDropdown.value = 0;
            OnCustomerTypeChanged(0);
        }

        SetAllCustomerInputFieldsInteractable(false); // Vô hiệu hóa tất cả các trường input ngoại trừ SĐT
        if (customerPhoneInputField != null) customerPhoneInputField.interactable = true; // SĐT luôn tương tác để tra cứu mới
        
        if (customerLookupStatusText != null) customerLookupStatusText.text = "";
        Debug.Log("Đã xóa thông tin khách hàng hiện tại.");
        OnCustomerInfoUpdated?.Invoke(); // Kích hoạt event
    }

    // Phương thức để lấy dữ liệu cuối cùng từ UI trước khi hoàn tất giao dịch
    public CustomerData GetCustomerDataFromUI()
    {
        // Nếu đã có currentCustomer (đã tra cứu hoặc đã tạo mới), thì dùng nó.
        // Ngược lại, tạo một CustomerData mới từ UI (để xử lý trường hợp chưa tra cứu).
        // Đây là bản tóm tắt dữ liệu từ UI, không phải bản lưu vào Firestore.
        // Logic lưu vào Firestore sẽ nằm trong SalesFinalizeTransaction.
        
        // Luôn tạo một đối tượng mới từ UI để đảm bảo dữ liệu mới nhất
        return new CustomerData
        {
            customerId = _currentCustomer?.customerId ?? "", // Giữ ID nếu đã có
            phone = customerPhoneInputField.text.Trim() ?? "",
            name = customerNameInputField.text.Trim() ?? "",
            address = customerAddressInputField.text.Trim() ?? "",
            taxId = customerTaxIdInputField.text.Trim() ?? "",
            companyName = customerCompanyNameInputField.text.Trim() ?? "",
            customerType = customerTypeDropdown.options[customerTypeDropdown.value].text ?? "",
            idNumber = customerIdNumberInputField.text.Trim() ?? ""
        };
    }

    // Phương thức để lấy dữ liệu _currentCustomer (sau khi đã tra cứu/lưu vào Firestore)
    public CustomerData GetCurrentCustomerData()
    {
        return _currentCustomer;
    }

    // Phương thức để đặt _currentCustomer (sau khi đã lưu vào Firestore trong FinalizeTransaction)
    public void SetCurrentCustomerData(CustomerData customer)
    {
        _currentCustomer = customer;
        OnCustomerInfoUpdated?.Invoke(); // Kích hoạt event
    }
}