// File: AddProductPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions; // Cần thiết cho ContinueWithOnMainThread
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Cần cho Autocomplete

public class AddProductPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelRoot; // Kéo GameObject gốc của AddProductPanel vào đây
    public TMP_InputField productNameInputField;
    public TMP_InputField unitInputField;
    public TMP_InputField priceInputField;
    public TMP_InputField importPriceInputField;
    public TMP_InputField barcodeInputField;
    public TMP_InputField imageURLInputField; // Tùy chọn
    public TMP_InputField initialStockInputField;

    // THAY THẾ: Sử dụng InputField cho Category và Manufacturer
    public TMP_InputField categoryInputField; // <-- SỬA TẠI ĐÂY
    public TMP_InputField manufacturerInputField; // <-- SỬA TẠI ĐÂY

    // THÊM: Tham chiếu tới các container Suggestion và Prefab Item Suggestion
    [Header("Autocomplete UI (Gợi ý)")]
    public RectTransform categorySuggestionContainer;
    public RectTransform manufacturerSuggestionContainer;
    public GameObject suggestionItemPrefab; // Prefab của 1 dòng gợi ý

    public Button confirmAddButton;
    public Button cancelAddButton;

    private FirebaseFirestore db;
    private Firebase.Auth.FirebaseUser currentUser;
    private CollectionReference userProductsCollection;

    private Action onProductAddedCallback; // Callback để thông báo cho InventoryManager

    // Dữ liệu gợi ý sẽ được lưu tạm ở đây
    private List<string> _existingCategories = new List<string>();
    private List<string> _existingManufacturers = new List<string>();

    void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (confirmAddButton != null) confirmAddButton.onClick.AddListener(OnConfirmAddButtonClicked);
        if (cancelAddButton != null) cancelAddButton.onClick.AddListener(HidePanel);

        db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
    }

    void OnDestroy()
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
        // Gỡ listener Autocomplete khi bị hủy
        categoryInputField?.onValueChanged.RemoveAllListeners();
        manufacturerInputField?.onValueChanged.RemoveAllListeners();
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        Firebase.Auth.FirebaseUser newUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                Debug.Log($"AddProductPanelManager: UserProductsCollection set for UID: {currentUser.UserId}");
            }
            else
            {
                userProductsCollection = null;
                Debug.Log("AddProductPanelManager: User logged out.");
            }
        }
    }

    // Hàm này được gọi từ InventoryManager để hiển thị panel
    public void ShowPanel(Action callback, List<string> existingCategories, List<string> existingManufacturers)
    {
        onProductAddedCallback = callback;
        _existingCategories = existingCategories;
        _existingManufacturers = existingManufacturers;

        // Reset các trường input
        productNameInputField.text = "";
        unitInputField.text = "";
        priceInputField.text = "";
        importPriceInputField.text = "";
        barcodeInputField.text = "";
        imageURLInputField.text = "";

        // Thiết lập giá trị mặc định "0"
        if (initialStockInputField != null) initialStockInputField.text = "0"; // <-- Đặt mặc định là "0"

        categoryInputField.text = ""; // <-- Reset Input Field mới
        manufacturerInputField.text = ""; // <-- Reset Input Field mới

        // Thiết lập Autocomplete
        SetupAutocompleteListeners();

        // Đảm bảo gợi ý đang ẩn khi panel mở
        if(categorySuggestionContainer != null) categorySuggestionContainer.gameObject.SetActive(false);
        if(manufacturerSuggestionContainer != null) manufacturerSuggestionContainer.gameObject.SetActive(false);


        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    // Đã loại bỏ phương thức PopulateDropdowns

    private void SetupAutocompleteListeners()
    {
        // Xóa listener cũ để tránh bị gọi nhiều lần
        categoryInputField?.onValueChanged.RemoveAllListeners();
        manufacturerInputField?.onValueChanged.RemoveAllListeners();

        // Thêm listener mới cho Category
        categoryInputField?.onValueChanged.AddListener((text) =>
            GenerateSuggestions(text, _existingCategories, categorySuggestionContainer, categoryInputField));

        // Thêm listener mới cho Manufacturer
        manufacturerInputField?.onValueChanged.AddListener((text) =>
            GenerateSuggestions(text, _existingManufacturers, manufacturerSuggestionContainer, manufacturerInputField));
    }

    // HÀM XỬ LÝ GỢI Ý (Conceptual Autocomplete Logic)
    private void GenerateSuggestions(string searchText, List<string> sourceList, RectTransform container, TMP_InputField targetInput)
    {
        // Xóa gợi ý cũ
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        if (string.IsNullOrEmpty(searchText) || suggestionItemPrefab == null)
        {
            container.gameObject.SetActive(false);
            return;
        }

        string lowerSearchText = searchText.ToLower();
        var suggestions = sourceList
            .Where(s => !string.IsNullOrEmpty(s) && s.ToLower().Contains(lowerSearchText))
            .ToList();

        if (suggestions.Count > 0)
        {
            container.gameObject.SetActive(true);
            foreach (var suggestion in suggestions.Take(5)) // Giới hạn 5 gợi ý
            {
                GameObject suggestionGO = Instantiate(suggestionItemPrefab, container);
                TMP_Text suggestionText = suggestionGO.GetComponentInChildren<TMP_Text>();
                Button suggestionButton = suggestionGO.GetComponent<Button>();

                if (suggestionText != null) suggestionText.text = suggestion;

                if (suggestionButton != null)
                {
                    string suggestedValue = suggestion; // Tạo biến cục bộ để Closure bắt đúng giá trị
                    suggestionButton.onClick.AddListener(() => {
                        targetInput.text = suggestedValue; // Gán giá trị vào Input Field
                        container.gameObject.SetActive(false); // Ẩn danh sách gợi ý
                    });
                }
            }
        }
        else
        {
            container.gameObject.SetActive(false);
        }
    }


    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        // Xóa gợi ý khi ẩn panel
        if(categorySuggestionContainer != null)
            foreach (Transform child in categorySuggestionContainer) Destroy(child.gameObject);
        if(manufacturerSuggestionContainer != null)
            foreach (Transform child in manufacturerSuggestionContainer) Destroy(child.gameObject);
    }

    private async void OnConfirmAddButtonClicked()
    {
        if (userProductsCollection == null)
        {
            Debug.LogError("UserProductsCollection chưa được thiết lập. Người dùng chưa đăng nhập?");
            StatusPopupManager.Instance.ShowPopup("Lỗi: Vui lòng đăng nhập để thêm sản phẩm.");
            return;
        }

        // Lấy dữ liệu từ các trường input
        string productName = productNameInputField.text.Trim();
        string unit = unitInputField.text.Trim();
        long price = 0;
        long importPrice = 0;
        string barcode = barcodeInputField.text.Trim();
        string imageUrl = imageURLInputField.text.Trim();
        long initialStock = 0;

        // LẤY GIÁ TRỊ TỪ INPUT FIELD MỚI
        string category = categoryInputField.text.Trim();
        string manufacturer = manufacturerInputField.text.Trim();

        // Kiểm tra validation
        if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(unit))
        {
            StatusPopupManager.Instance.ShowPopup("Tên sản phẩm và Đơn vị là bắt buộc.");
            return;
        }

        // Kiểm tra Giá Bán và Giá Nhập
        if (!long.TryParse(priceInputField.text, out price) || price < 0)
        {
            StatusPopupManager.Instance.ShowPopup("Giá bán không hợp lệ (phải là số nguyên không âm).");
            return;
        }
        if (!long.TryParse(importPriceInputField.text, out importPrice) || importPrice < 0)
        {
            StatusPopupManager.Instance.ShowPopup("Giá nhập không hợp lệ (phải là số nguyên không âm).");
            return;
        }

        // Kiểm tra Tồn kho ban đầu (Cho phép để trống hoặc 0)
        string stockText = initialStockInputField.text.Trim();
        if (string.IsNullOrEmpty(stockText))
        {
            initialStock = 0; // <-- Đặt mặc định là 0 nếu để trống
        }
        else if (!long.TryParse(stockText, out initialStock) || initialStock < 0)
        {
            StatusPopupManager.Instance.ShowPopup("Tồn kho ban đầu không hợp lệ (phải là số nguyên không âm).");
            return;
        }

        // Tạo đối tượng ProductData mới
        ProductData newProduct = new ProductData
        {
            productName = productName,
            unit = unit,
            price = price,
            importPrice = importPrice,
            barcode = barcode,
            imageUrl = imageUrl,
            stock = initialStock,
            category = category,
            manufacturer = manufacturer
        };

        confirmAddButton.interactable = false;
        cancelAddButton.interactable = false;

        if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
                return;
            }

        try
        {
            // Thêm sản phẩm mới vào Firestore.
            DocumentReference docRef = await userProductsCollection.AddAsync(newProduct);
            newProduct.productId = docRef.Id;

            Debug.Log($"Đã thêm sản phẩm mới thành công: {newProduct.productName} với ID: {newProduct.productId}");
            StatusPopupManager.Instance.ShowPopup("Thêm sản phẩm thành công!");

            onProductAddedCallback?.Invoke(); // Gọi callback để InventoryManager làm mới

            await Task.Delay(1000);
            HidePanel();
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi thêm sản phẩm: {e.Message}";
                    if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
                    {
                        errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
                    }
                    StatusPopupManager.Instance.ShowPopup(errorMessage);
                    Debug.LogError($"Lỗi khi thêm sản phẩm mới: {e.Message}");
        }
        finally
        {
            confirmAddButton.interactable = true;
            cancelAddButton.interactable = true;
        }
    }
}