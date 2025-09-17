// File: AddProductPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions; // Cần thiết cho ContinueWithOnMainThread
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

    // Sử dụng Dropdown cho Category và Manufacturer
    public TMP_Dropdown categoryDropdown; // Có thể để user nhập hoặc chọn từ danh sách có sẵn
    public TMP_Dropdown manufacturerDropdown; // Có thể để user nhập hoặc chọn từ danh sách có sẵn

    public Button confirmAddButton;
    public Button cancelAddButton;
    // THAY ĐỔI: Bỏ biến này vì sẽ dùng StatusPopupManager
    // public TMP_Text statusMessageText;

    // THAY ĐỔI: Loại bỏ tham chiếu trực tiếp đến StatusPopupManager vì nó là Singleton
    // public StatusPopupManager statusPopupManager;

    private FirebaseFirestore db;
    private Firebase.Auth.FirebaseUser currentUser;
    private CollectionReference userProductsCollection;

    private Action onProductAddedCallback; // Callback để thông báo cho InventoryManager

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

        // Reset các trường input
        productNameInputField.text = "";
        unitInputField.text = "";
        priceInputField.text = "";
        importPriceInputField.text = "";
        barcodeInputField.text = "";
        imageURLInputField.text = "";
        initialStockInputField.text = "0"; // Mặc định tồn kho ban đầu là 0

        // THAY ĐỔI: Bỏ dòng này
        // statusMessageText.text = "";

        PopulateDropdowns(existingCategories, existingManufacturers);

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    private void PopulateDropdowns(List<string> categories, List<string> manufacturers)
    {
        if (categoryDropdown != null)
        {
            categoryDropdown.ClearOptions();
            List<string> categoryOptions = new List<string> { "Chọn hoặc nhập danh mục" };
            if (categories != null) categoryOptions.AddRange(categories);
            categoryDropdown.AddOptions(categoryOptions);
            // Thêm listener để xử lý khi người dùng nhập text mới hoặc chọn từ dropdown
            categoryDropdown.onValueChanged.RemoveAllListeners();
            categoryDropdown.onValueChanged.AddListener((int index) => {
                // Tùy chọn: nếu người dùng chọn mục đầu tiên "Chọn hoặc nhập...", bạn có thể reset InputField hoặc xử lý khác
            });
        }

        if (manufacturerDropdown != null)
        {
            manufacturerDropdown.ClearOptions();
            List<string> manufacturerOptions = new List<string> { "Chọn hoặc nhập nhà sản xuất" };
            if (manufacturers != null) manufacturerOptions.AddRange(manufacturers);
            manufacturerDropdown.AddOptions(manufacturerOptions);
            manufacturerDropdown.onValueChanged.RemoveAllListeners();
            manufacturerDropdown.onValueChanged.AddListener((int index) => {
                // Tùy chọn: xử lý tương tự như categoryDropdown
            });
        }
    }


    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        // THAY ĐỔI: Bỏ dòng này
        // statusMessageText.text = "";
    }

    private async void OnConfirmAddButtonClicked()
    {
        if (userProductsCollection == null)
        {
            Debug.LogError("UserProductsCollection chưa được thiết lập. Người dùng chưa đăng nhập?");
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
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
        string category = "";
        string manufacturer = "";

        // Lấy giá trị từ dropdown hoặc input text (nếu bạn có cả 2)
        if (categoryDropdown != null && categoryDropdown.value > 0)
        {
            category = categoryDropdown.options[categoryDropdown.value].text;
        }

        if (manufacturerDropdown != null && manufacturerDropdown.value > 0)
        {
            manufacturer = manufacturerDropdown.options[manufacturerDropdown.value].text;
        }

        // Kiểm tra validation
        if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(unit))
        {
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
            StatusPopupManager.Instance.ShowPopup("Tên sản phẩm và Đơn vị là bắt buộc.");
            return;
        }
        if (!long.TryParse(priceInputField.text, out price) || price < 0)
        {
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
            StatusPopupManager.Instance.ShowPopup("Giá bán không hợp lệ.");
            return;
        }
        if (!long.TryParse(importPriceInputField.text, out importPrice) || importPrice < 0)
        {
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
            StatusPopupManager.Instance.ShowPopup("Giá nhập không hợp lệ.");
            return;
        }
        if (!long.TryParse(initialStockInputField.text, out initialStock) || initialStock < 0)
        {
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
            StatusPopupManager.Instance.ShowPopup("Tồn kho ban đầu không hợp lệ.");
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

        // THAY ĐỔI: Bỏ dòng này
        // statusMessageText.text = "Đang thêm sản phẩm...";
        // Bạn có thể thêm một Loading Panel hoặc chỉ làm mờ màn hình ở đây
        confirmAddButton.interactable = false;
        cancelAddButton.interactable = false;

        if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
                return;
            }

        try
        {
            // Thêm sản phẩm mới vào Firestore. Firestore sẽ tự động tạo một Document ID mới.
            DocumentReference docRef = await userProductsCollection.AddAsync(newProduct);
            newProduct.productId = docRef.Id; // Gán lại ID cho đối tượng cục bộ

            Debug.Log($"Đã thêm sản phẩm mới thành công: {newProduct.productName} với ID: {newProduct.productId}");
            // THAY ĐỔI: Sử dụng StatusPopupManager.Instance
            StatusPopupManager.Instance.ShowPopup("Thêm sản phẩm thành công!");

            onProductAddedCallback?.Invoke(); // Gọi callback để InventoryManager làm mới

            await Task.Delay(1000); // Đợi 1 giây để người dùng thấy thông báo
            HidePanel();
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi thêm sản phẩm: {e.Message}";
                    if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
                    {
                        errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
                    }
                    StatusPopupManager.Instance.ShowPopup(errorMessage); // GỌI POPUP
                    Debug.LogError($"Lỗi khi thêm sản phẩm mới: {e.Message}");
        }
        finally
        {
            confirmAddButton.interactable = true;
            cancelAddButton.interactable = true;
        }
    }
}