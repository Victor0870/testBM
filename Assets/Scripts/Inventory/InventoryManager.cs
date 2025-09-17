using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq; // Quan trọng để sử dụng LINQ (Where, OrderBy, ToList)

// THÊM: Các using mới cho việc kiểm tra tính năng
using static ShopSessionData; // Để truy cập CachedShopSettings và GlobalAppConfig
// using static AuthManager; // Đã có GlobalAppConfig trong AuthManager, có thể truy cập qua AuthManager.GlobalAppConfig

public class InventoryManager : MonoBehaviour
{
    [Header("Firebase")]
    public string sampleIndustryKey = "pharma"; // ID ngành mặc định cho dữ liệu mẫu

    [Header("UI References")]
    public GameObject inventoryContentPanel; // Panel chứa danh sách tồn kho chính
    public GameObject sampleInventorySelectionPanel; // Panel để chọn kho mẫu
    public TMP_Text inventoryStatusText; // Text hiển thị trạng thái tồn kho
    public TMP_Dropdown industryDropdown; // Dropdown để chọn ngành hàng
    public Button createFromSampleButton;
    public Button skipSampleButton;
    public GameObject sampleLoadingPanel;
    public TMP_Text sampleStatusText;

    [Header("Filter Dropdowns")]
    public TMP_Dropdown categoryFilterDropdown;
    public TMP_Dropdown manufacturerFilterDropdown;

    [Header("Inventory Summary UI")]
    public TMP_Text totalInventoryValueText; // Text để hiển thị tổng giá trị tồn kho
    public TMP_Text totalInventoryQuantityText; // Text để hiển thị tổng số lượng tồn kho

    [Header("Search & Sort UI")]
    public TMP_InputField searchInputField; // InputField để nhập chuỗi tìm kiếm
    public TMP_Dropdown sortDropdown; // Dropdown để chọn tùy chọn sắp xếp

    [Header("Product Item Display")]
    public GameObject productItemPrefab; // Kéo Prefab của ProductUIItem vào đây
    public Transform productListContentParent; // Kéo Content Transform của Scroll View vào đây

    [Header("Add New Product UI")]
    public GameObject addProductPanel; // Panel để thêm sản phẩm mới
    // Thêm nút mở panel thêm sản phẩm mới
    public Button openAddProductPanelButton; // THÊM DÒNG NÀY

    [Header("Edit Product UI")]
    public GameObject editProductPanel; // Panel chỉnh sửa sản phẩm
    public TMP_Text editProductIdText; // Hiển thị Product ID (chỉ đọc)
    public TMP_InputField editProductNameInput;
    public TMP_InputField editProductUnitInput;
    public TMP_InputField editProductPriceInput;
    public TMP_InputField editProductImportPriceInput;
    public TMP_InputField editProductBarcodeInput;
    public TMP_InputField editProductImageUrlInput;
    public TMP_InputField editProductStockInput;
    public TMP_InputField editProductCategoryInput;
    public TMP_InputField editProductManufacturerInput;
    public Button saveEditProductButton;
    public Button cancelEditProductButton;
    public Button deleteProductButton; // Nút xóa sản phẩm

    private ProductData currentEditingProduct;

    [Header("Import Stock Panel")]
    public ImportStockPanelManager importStockPanelManager;

    private FirebaseUser currentUser;
    private FirebaseFirestore db;
    private ListenerRegistration productListenerRegistration;
    private List<ProductData> allProducts = new List<ProductData>();
    private string currentCollectionPath;

    void Start()
    {
        InitializeFirebase();
        SetupUIListeners();
        PopulateSortDropdown();

        /// GỌI HÀM KIỂM TRA QUYỀN TRUY CẬP TÍNH NĂNG KHI SCENE KHỞI ĐỘNG

        CheckFeatureAccess();

    }

    private void OnDestroy()
    {
        if (productListenerRegistration != null)
        {
            productListenerRegistration.Dispose();
            productListenerRegistration = null;
        }
    }

    void InitializeFirebase()
    {
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth.DefaultInstance.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != currentUser)
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            bool signedIn = currentUser != null; // Đã đổi cách kiểm tra signedIn
            if (!signedIn && currentUser == null) // Đã đăng xuất
            {
                Debug.Log("Người dùng đã đăng xuất.");
                // Xử lý khi người dùng đăng xuất (ví dụ: quay lại màn hình đăng nhập)
            }
            if (signedIn)
            {
                Debug.Log($"Người dùng đã đăng nhập: {currentUser.DisplayName ?? currentUser.Email}");
                CheckUserInventoryStatus();
            }
            /// GỌI HÀM KIỂM TRA QUYỀN TRUY CẬP TÍNH NĂNG MỖI KHI TRẠNG THÁI ĐĂNG NHẬP THAY ĐỔI (ví dụ user đăng nhập lại)
            CheckFeatureAccess();

        }
    }

    void SetupUIListeners()
    {
        createFromSampleButton?.onClick.AddListener(() => CreateInventoryFromSample(sampleIndustryKey));
        skipSampleButton?.onClick.AddListener(SkipSampleInventory);

        // Gán listener cho nút mở panel thêm sản phẩm mới
        //if (openAddProductPanelButton != null) // THÊM DÒNG NÀY
        {
            openAddProductPanelButton.onClick.AddListener(ShowAddProductPanel); // THÊM DÒNG NÀY
        }

        saveEditProductButton?.onClick.AddListener(SaveEditedProduct);
        cancelEditProductButton?.onClick.AddListener(() => editProductPanel.SetActive(false));
        deleteProductButton?.onClick.AddListener(DeleteProduct);

        categoryFilterDropdown?.onValueChanged.AddListener(delegate { ApplyFiltersAndSearch(); });
        manufacturerFilterDropdown?.onValueChanged.AddListener(delegate { ApplyFiltersAndSearch(); });

        searchInputField?.onEndEdit.AddListener(delegate { ApplyFiltersAndSearch(); });
        searchInputField?.onValueChanged.AddListener(delegate { ApplyFiltersAndSearch(); });

        sortDropdown?.onValueChanged.AddListener(delegate { ApplyFiltersAndSearch(); });
    }

    void PopulateSortDropdown()
    {
        if (sortDropdown == null)
        {
            Debug.LogError("Sort Dropdown chưa được gán trong Inspector của InventoryManager.");
            return;
        }

        sortDropdown.ClearOptions();

        List<string> sortOptions = new List<string>
        {
            "Tên (A-Z)",
            "Tên (Z-A)",
            "Giá (thấp đến cao)",
            "Giá (cao đến thấp)",
            "Tồn kho (ít đến nhiều)",
            "Tồn kho (nhiều đến ít)",
            "Mới nhất"
        };

        sortDropdown.AddOptions(sortOptions);

        sortDropdown.value = 0;
        sortDropdown.RefreshShownValue();
    }

    /// THÊM PHƯƠNG THỨC KIỂM TRA QUYỀN TRUY CẬP TÍNH NĂNG
    private void CheckFeatureAccess()
    {
        // Lấy tên gói hiện tại của người dùng từ cache
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType; //

        /// Kiểm tra xem GlobalAppConfig và AppPackageConfig có tồn tại không
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null) //
        {
            Debug.LogWarning("InventoryManager: Cấu hình gói hoặc App Package Config chưa được tải. Vô hiệu hóa các tính năng tồn kho."); //
            SetInventoryFeaturesInteractable(false, "Không thể tải cấu hình gói ứng dụng."); //
            return; //
        }

        /// Kiểm tra xem gói hiện tại có tính năng Inventory không
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory); //

        if (hasInventoryFeature)
        {
            SetInventoryFeaturesInteractable(true); // Kích hoạt các tính năng tồn kho
            Debug.Log($"InventoryManager: Gói '{currentPackageName}' có quyền truy cập tính năng Tồn kho."); //
        }
        else
        {
            SetInventoryFeaturesInteractable(false, $"Tính năng quản lý tồn kho yêu cầu gói Nâng cao trở lên. Gói hiện tại: '{currentPackageName}'. Vui lòng nâng cấp gói để sử dụng."); // Vô hiệu hóa và hiển thị thông báo
            Debug.LogWarning($"InventoryManager: Gói '{currentPackageName}' không có quyền truy cập tính năng Tồn kho."); //
        }
    }

    /// THÊM PHƯƠNG THỨC ĐỂ ĐIỀU KHIỂN SỰ TƯƠNG TÁC CỦA CÁC PHẦN TỬ UI LIÊN QUAN ĐẾN TỒN KHO
    private void SetInventoryFeaturesInteractable(bool interactable, string message = null)
    {
        // Các nút thêm/sửa/xóa sản phẩm
        //if (openAddProductPanelButton != null) openAddProductPanelButton.interactable = interactable; [cite: 6]
        //if (saveEditProductButton != null) saveEditProductButton.interactable = interactable; [cite: 6]
        //if (cancelEditProductButton != null) cancelEditProductButton.interactable = interactable; [cite: 6]
        //if (deleteProductButton != null) deleteProductButton.interactable = interactable; [cite: 6]
        if (importStockPanelManager != null) importStockPanelManager.SetInteractable(interactable); // Nếu ImportStockPanelManager có hàm SetInteractable

        // Các dropdown lọc
        //if (categoryFilterDropdown != null) categoryFilterDropdown.interactable = interactable; [cite: 6]
        //if (manufacturerFilterDropdown != null) manufacturerFilterDropdown.interactable = interactable; [cite: 6]
        //if (searchInputField != null) searchInputField.interactable = interactable; [cite: 6]
        //if (sortDropdown != null) sortDropdown.interactable = interactable; [cite: 6]

        // Các nút tạo/bỏ qua kho mẫu (nếu chỉ hiển thị khi không có kho, hãy xử lý riêng)
        //createFromSampleButton.interactable = interactable; [cite: 6]
        //skipSampleButton.interactable = interactable; [cite: 6]

        /// Nếu không tương tác, có thể hiển thị một overlay hoặc thông báo
        if (!interactable && !string.IsNullOrEmpty(message))
        {
            // Tránh hiển thị popup liên tục nếu đã có một popup tương tự
            // StatusPopupManager.Instance.ShowPopup(message); // Có thể gây phiền nếu popup liên tục
        }

        // Vô hiệu hóa toàn bộ panel nội dung nếu không có quyền
        if (inventoryContentPanel != null)
        {
            CanvasGroup canvasGroup = inventoryContentPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = inventoryContentPanel.AddComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.5f; // Làm mờ khi không tương tác
        }
    }

    void CheckUserInventoryStatus()
    {
        if (currentUser == null) return;

        string userId = currentUser.UserId;
        db.Collection("shops").Document(userId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Firebase.FirebaseException firebaseEx = task.Exception.GetBaseException() as Firebase.FirebaseException;
                string errorMessage = "Lỗi khi tải dữ liệu kho.";
                if (firebaseEx != null && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
                {
                    errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
                }
                else if (firebaseEx != null)
                {
                    errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                }

                Debug.LogError($"Lỗi khi kiểm tra trạng thái kho của người dùng: {task.Exception}");
                StatusPopupManager.Instance.ShowPopup(errorMessage);
                inventoryStatusText.text = errorMessage;
                return;
            }

            DocumentSnapshot snapshot = task.Result;
            if (snapshot.Exists && snapshot.ContainsField("currentInventoryPath"))
            {
                currentCollectionPath = snapshot.GetValue<string>("currentInventoryPath");
                Debug.Log($"Người dùng đã có kho tại: {currentCollectionPath}");
                sampleInventorySelectionPanel.SetActive(false);
                inventoryContentPanel.SetActive(true);
                ListenForProductChanges(currentCollectionPath);
            }
            else
            {
                Debug.Log("Người dùng chưa có kho, hiển thị tùy chọn tạo kho mẫu.");
                sampleInventorySelectionPanel.SetActive(true);
                inventoryContentPanel.SetActive(false);
                PopulateIndustryDropdown();
            }
        });
    }

    void PopulateIndustryDropdown()
    {
        industryDropdown.ClearOptions();
        List<string> options = new List<string> { "Chọn ngành hàng...", "Pharma", "Electronics", "Retail" };
        industryDropdown.AddOptions(options);
    }

    async void CreateInventoryFromSample(string industryKey)
    {
        if (currentUser == null) return;

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            sampleLoadingPanel.SetActive(false);
            SetInteractableUI(true);
            return;
        }

        sampleLoadingPanel.SetActive(true);
        sampleStatusText.text = "Đang tạo kho từ dữ liệu mẫu...";
        SetInteractableUI(false);

        try
        {
            string userId = currentUser.UserId;
            string samplePath = $"sample_inventories/{industryKey}/products";
            string userInventoryPath = $"shops/{userId}/products";

            CollectionReference sampleCollectionRef = db.Collection(samplePath);
            QuerySnapshot sampleSnapshot = await sampleCollectionRef.GetSnapshotAsync();

            if (!sampleSnapshot.Documents.Any())
            {
                sampleStatusText.text = $"Không tìm thấy dữ liệu mẫu cho ngành: {industryKey}";
                Debug.LogError($"Không tìm thấy dữ liệu mẫu tại {samplePath}");
                return;
            }

            WriteBatch batch = db.StartBatch();
            CollectionReference userProductsRef = db.Collection(userInventoryPath);

            foreach (DocumentSnapshot doc in sampleSnapshot.Documents)
            {
                ProductData product = doc.ConvertTo<ProductData>();
                product.productId = doc.Id;

                DocumentReference userProductDocRef = userProductsRef.Document(doc.Id);
                batch.Set(userProductDocRef, product);
            }

            await batch.CommitAsync();

            DocumentReference userDocRef = db.Collection("shops").Document(userId);
            await userDocRef.SetAsync(new Dictionary<string, object>
            {
                { "currentInventoryPath", userInventoryPath },
                { "createdAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            currentCollectionPath = userInventoryPath;
            sampleStatusText.text = "Tạo kho thành công!";
            Debug.Log($"Đã tạo kho từ dữ liệu mẫu cho người dùng {userId} tại {userInventoryPath}");

            sampleInventorySelectionPanel.SetActive(false);
            inventoryContentPanel.SetActive(true);
            ListenForProductChanges(currentCollectionPath);

        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
            }
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            sampleStatusText.text = errorMessage;
            Debug.LogError($"Lỗi khi tạo kho từ dữ liệu mẫu: {e}");
        }
        finally
        {
            sampleLoadingPanel.SetActive(false);
            SetInteractableUI(true);
        }
    }

    async void SkipSampleInventory()
    {
        if (currentUser == null) return;

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            SetInteractableUI(true);
            return;
        }

        SetInteractableUI(false);
        string userId = currentUser.UserId;
        string userInventoryPath = $"shops/{userId}/products";

        try
        {
            DocumentReference userDocRef = db.Collection("shops").Document(userId);
            await userDocRef.SetAsync(new Dictionary<string, object>
            {
                { "currentInventoryPath", userInventoryPath },
                { "createdAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);

            currentCollectionPath = userInventoryPath;
            Debug.Log($"Người dùng đã bỏ qua kho mẫu và tạo kho rỗng tại {userInventoryPath}");

            sampleInventorySelectionPanel.SetActive(false);
            inventoryContentPanel.SetActive(true);
            ListenForProductChanges(currentCollectionPath);
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi bỏ qua kho mẫu: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
            }
            Debug.LogError(errorMessage);
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            inventoryStatusText.text = errorMessage;
        }
        finally
        {
            SetInteractableUI(true);
        }
    }

    void SetInteractableUI(bool interactable)
    {
        createFromSampleButton.interactable = interactable;
        skipSampleButton.interactable = interactable;
        CanvasGroup canvasGroup = sampleInventorySelectionPanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }

    public void UpdateInventoryUI(List<ProductData> productsToDisplay)
    {
        foreach (Transform child in productListContentParent)
        {
            Destroy(child.gameObject);
        }

        if (productsToDisplay != null && productsToDisplay.Count > 0)
        {
            foreach (ProductData product in productsToDisplay)
            {
                GameObject productItemGO = Instantiate(productItemPrefab, productListContentParent);
                ProductUIItem uiItem = productItemGO.GetComponent<ProductUIItem>();
                if (uiItem != null)
                {
                    uiItem.SetProductData(product);
                    uiItem.OnEditActionRequested.AddListener(HandleEditProductRequest);
                    uiItem.OnImportStockRequested.AddListener(HandleImportStockRequest);
                }
                else
                {
                    Debug.LogWarning("Prefab productItemPrefab không có script ProductUIItem. Vui lòng kiểm tra lại!");
                }
            }
        }
        else
        {
            Debug.Log("Không có sản phẩm nào để hiển thị sau khi áp dụng bộ lọc/tìm kiếm.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Không tìm thấy sản phẩm phù hợp với bộ lọc/tìm kiếm.";
        }

        CalculateInventorySummary(productsToDisplay);
    }

    void ListenForProductChanges(string collectionPath)
    {
        if (string.IsNullOrEmpty(collectionPath))
        {
            Debug.LogError("Đường dẫn collection rỗng, không thể lắng nghe thay đổi.");
            return;
        }

        if (productListenerRegistration != null)
        {
            productListenerRegistration.Dispose();
        }

        CollectionReference productsRef = db.Collection(collectionPath);

        productListenerRegistration = productsRef.Listen(snapshot =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                allProducts.Clear();
                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        ProductData product = document.ConvertTo<ProductData>();
                        product.productId = document.Id;
                        allProducts.Add(product);
                    }
                }
                Debug.Log($"Đã cập nhật {allProducts.Count} sản phẩm từ Firestore.");
                ApplyFiltersAndSearch();
                PopulateFilterDropdowns();
            });
        });
        Debug.Log($"Bắt đầu lắng nghe thay đổi sản phẩm tại: {collectionPath}");
    }

    void PopulateFilterDropdowns()
    {
        List<string> categories = allProducts.Select(p => p.category).Distinct().OrderBy(c => c).ToList();
        List<string> manufacturers = allProducts.Select(p => p.manufacturer).Distinct().OrderBy(m => m).ToList();

        categoryFilterDropdown.ClearOptions();
        categoryFilterDropdown.AddOptions(new List<string> { "Tất cả Danh mục" });
        categoryFilterDropdown.AddOptions(categories);

        manufacturerFilterDropdown.ClearOptions();
        manufacturerFilterDropdown.AddOptions(new List<string> { "Tất cả Nhà sản xuất" });
        manufacturerFilterDropdown.AddOptions(manufacturers);
    }


    void ApplyFiltersAndSearch()
    {
        IEnumerable<ProductData> filteredProducts = allProducts;

        if (categoryFilterDropdown != null && categoryFilterDropdown.value > 0)
        {
            string selectedCategory = categoryFilterDropdown.options[categoryFilterDropdown.value].text;
            if (selectedCategory != "Tất cả Danh mục")
            {
                filteredProducts = filteredProducts.Where(p => p.category == selectedCategory);
            }
        }

        if (manufacturerFilterDropdown != null && manufacturerFilterDropdown.value > 0)
        {
            string selectedManufacturer = manufacturerFilterDropdown.options[manufacturerFilterDropdown.value].text;
            if (selectedManufacturer != "Tất cả Nhà sản xuất")
            {
                filteredProducts = filteredProducts.Where(p => p.manufacturer == selectedManufacturer);
            }
        }

        if (searchInputField != null && !string.IsNullOrEmpty(searchInputField.text))
        {
            string searchText = searchInputField.text.ToLower();
            filteredProducts = filteredProducts.Where(p =>
                p.productName.ToLower().Contains(searchText) ||
                p.barcode.ToLower().Contains(searchText)
            );
        }

        if (sortDropdown != null)
        {
            switch (sortDropdown.value)
            {
                case 0:
                    filteredProducts = filteredProducts.OrderBy(p => p.productName);
                    break;
                case 1:
                    filteredProducts = filteredProducts.OrderByDescending(p => p.productName);
                    break;
                case 2:
                    filteredProducts = filteredProducts.OrderBy(p => p.price);
                    break;
                case 3:
                    filteredProducts = filteredProducts.OrderByDescending(p => p.price);
                    break;
                case 4:
                    filteredProducts = filteredProducts.OrderBy(p => p.stock);
                    break;
                case 5:
                    filteredProducts = filteredProducts.OrderByDescending(p => p.stock);
                    break;
                case 6:
                    filteredProducts = filteredProducts.OrderByDescending(p => p.productId);
                    break;
                default:
                    break;
            }
        }

        UpdateInventoryUI(filteredProducts.ToList());
    }

    void CalculateInventorySummary(List<ProductData> products)
    {
        long totalValue = 0;
        long totalQuantity = 0;

        foreach (var product in products)
        {
            totalValue += product.price * product.stock;
            totalQuantity += product.stock;
        }

        if (totalInventoryValueText != null)
        {
            totalInventoryValueText.text = $"{totalValue:N0} VNĐ";
        }
        if (totalInventoryQuantityText != null)
        {
            totalInventoryQuantityText.text = $" {totalQuantity:N0}";
        }

        if (inventoryStatusText != null)
        {
            if (products.Count == 0 && allProducts.Count > 0)
            {
                inventoryStatusText.text = "Không tìm thấy sản phẩm phù hợp với bộ lọc/tìm kiếm.";
            }
            else if (products.Count == 0 && allProducts.Count == 0)
            {
                inventoryStatusText.text = "Kho trống. Vui lòng thêm sản phẩm hoặc tạo từ kho mẫu.";
            }
            else
            {
                inventoryStatusText.text = $"Hiển thị {products.Count} sản phẩm.";
            }
        }
    }


    public void ShowAddProductPanel()
    {
        // AddProductPanelManager giờ sẽ quản lý việc hiển thị panel này,
        // bao gồm cả việc reset các trường input.
        // Cần đảm bảo addProductPanelManager đã được gán trong Inspector
        if (addProductPanel.GetComponent<AddProductPanelManager>() != null)
        {
            addProductPanel.GetComponent<AddProductPanelManager>().ShowPanel(
                null, // Callback có thể để null nếu InventoryManager không cần biết ngay
                allProducts.Select(p => p.category).Distinct().ToList(), // Truyền danh sách categories hiện có
                allProducts.Select(p => p.manufacturer).Distinct().ToList() // Truyền danh sách manufacturers hiện có
            );
        }
        else
        {
            Debug.LogError("AddProductPanelManager script không tìm thấy trên addProductPanel GameObject. Vui lòng kiểm tra lại.");
        }
    }

    private void HandleEditProductRequest(ProductData productToEdit)
    {
        Debug.Log($"Yêu cầu chỉnh sửa sản phẩm: {productToEdit.productName} (ID: {productToEdit.productId})");

        currentEditingProduct = productToEdit;

        editProductPanel.SetActive(true);

        if (editProductIdText != null) editProductIdText.text = $"ID: {productToEdit.productId}";
        if (editProductNameInput != null) editProductNameInput.text = productToEdit.productName;
        if (editProductUnitInput != null) editProductUnitInput.text = productToEdit.unit;
        if (editProductPriceInput != null) editProductPriceInput.text = productToEdit.price.ToString();
        if (editProductImportPriceInput != null) editProductImportPriceInput.text = productToEdit.importPrice.ToString();
        if (editProductBarcodeInput != null) editProductBarcodeInput.text = productToEdit.barcode;
        if (editProductImageUrlInput != null) editProductImageUrlInput.text = productToEdit.imageUrl;
        if (editProductStockInput != null) editProductStockInput.text = productToEdit.stock.ToString();
        if (editProductCategoryInput != null) editProductCategoryInput.text = productToEdit.category;
        if (editProductManufacturerInput != null) editProductManufacturerInput.text = productToEdit.manufacturer;
    }

    async void SaveEditedProduct()
    {
        if (currentUser == null || string.IsNullOrEmpty(currentCollectionPath) || currentEditingProduct == null)
        {
            Debug.LogError("Không thể lưu sản phẩm đã chỉnh sửa: Chưa đăng nhập, đường dẫn kho chưa thiết lập hoặc không có sản phẩm nào đang được chỉnh sửa.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        currentEditingProduct.productName = editProductNameInput.text;
        currentEditingProduct.unit = editProductUnitInput.text;
        currentEditingProduct.price = long.TryParse(editProductPriceInput.text, out long p) ? p : 0;
        currentEditingProduct.importPrice = long.TryParse(editProductImportPriceInput.text, out long ip) ? ip : 0;
        currentEditingProduct.barcode = editProductBarcodeInput.text;
        currentEditingProduct.imageUrl = editProductImageUrlInput.text;
        currentEditingProduct.stock = long.TryParse(editProductStockInput.text, out long s) ? s : 0;
        currentEditingProduct.category = editProductCategoryInput.text;
        currentEditingProduct.manufacturer = editProductManufacturerInput.text;

        if (string.IsNullOrEmpty(currentEditingProduct.productName))
        {
            Debug.LogError("Tên sản phẩm không được để trống.");
            return;
        }

        try
        {
            CollectionReference productsRef = db.Collection(currentCollectionPath);
            DocumentReference docRef = productsRef.Document(currentEditingProduct.productId);

            await docRef.SetAsync(currentEditingProduct);

            Debug.Log($"Đã lưu sản phẩm {currentEditingProduct.productName} (ID: {currentEditingProduct.productId}) thành công!");
            editProductPanel.SetActive(false);
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi lưu sản phẩm đã chỉnh sửa: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
            }
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            Debug.LogError(errorMessage);
        }
    }

    async void DeleteProduct()
    {
        if (currentUser == null || string.IsNullOrEmpty(currentCollectionPath) || currentEditingProduct == null)
        {
            Debug.LogError("Không thể xóa sản phẩm: Chưa đăng nhập, đường dẫn kho chưa thiết lập hoặc không có sản phẩm nào đang được chỉnh sửa.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        try
        {
            CollectionReference productsRef = db.Collection(currentCollectionPath);
            DocumentReference docRef = productsRef.Document(currentEditingProduct.productId);

            await docRef.DeleteAsync();

            Debug.Log($"Đã xóa sản phẩm {currentEditingProduct.productName} (ID: {currentEditingProduct.productId}) thành công!");
            editProductPanel.SetActive(false);
            currentEditingProduct = null;
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi xóa sản phẩm: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng. Vui lòng kiểm tra mạng của bạn.";
            }
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            Debug.LogError(errorMessage);
        }
    }

    private void HandleImportStockRequest(ProductData productToImport)
    {
        if (importStockPanelManager != null)
        {
            Debug.Log($"Yêu cầu nhập kho cho sản phẩm: {productToImport.productName} (ID: {productToImport.productId})");
            importStockPanelManager.ShowPanel(productToImport, () => {
                Debug.Log("Tồn kho đã được cập nhật từ ImportStockPanelManager.");
            });
        }
        else
        {
            Debug.LogError("ImportStockPanelManager chưa được gán trong Inspector của InventoryManager.");
        }
    }
}