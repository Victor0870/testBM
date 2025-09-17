// File: SalesCartManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth; // Cần cho FirebaseUser
using Firebase.Extensions; // Cần cho ContinueWithOnMainThread
using System;
using System.Collections.Generic;
using System.Linq; // Cần cho LINQ (FirstOrDefault, Any)

// Đảm bảo các using cần thiết từ SalesManager được đưa vào đây
using static ShopSessionData; // Để truy cập ShopSessionData.CachedShopSettings, GlobalAppConfig, AppPackageConfig

public class SalesCartManager : MonoBehaviour
{
    [Header("Cart & Add Product Area UI - SalesCartManager")]
    public GameObject cartItemsParent; // GameObject cha cho các prefab CartItemUI
    public GameObject cartItemPrefab; // Prefab cho từng sản phẩm trong giỏ hàng
    public GameObject addProductToCartMainButtonPrefab; // Prefab của nút "Thêm sản phẩm" chính

    public RectTransform cartAndAddProductAreaRect; // Tham chiếu RectTransform của Cart And Add product area root

    [Header("Product Selection Popup - SalesCartManager")]
    public GameObject productSelectionPopupRoot; // Kéo GameObject gốc của popup chọn sản phẩm vào đây (ban đầu INACTIVE)
    public TMP_InputField productSearchInputField;
    public Button scanBarcodeButton; // Placeholder
    public Button closeProductSelectionPopupButton; // Nút đóng popup

    public Transform productSearchResultsContentParent; // Kéo Content của Scroll View tìm kiếm vào đây
    public GameObject productSearchItemPrefab; // Kéo prefab ProductSearchUIItem.prefab vào đây

    [Header("Layout Settings (Controlled by Code)")]
    public float perProductHeight = 320f;   // Chiều cao cộng thêm cho mỗi sản phẩm trong giỏ
    public float addButtonHeight = 100f;    // Chiều cao cố định của nút "Thêm sản phẩm"

    // Tham chiếu đến StatusPopupManager (được truyền từ SalesManager chính)
    private StatusPopupManager _statusPopupManager;
    // Tham chiếu đến Firebase (được truyền từ SalesManager chính)
    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userProductsCollection;

    // Danh sách chứa tất cả sản phẩm từ Firestore
    private List<ProductData> _allUserProducts; // Sẽ được truyền vào hoặc lấy từ listener
    // Sản phẩm trong giỏ hàng
    private Dictionary<string, ProductData> _productsInCart = new Dictionary<string, ProductData>();
    private Dictionary<string, GameObject> _cartItemUIObjects = new Dictionary<string, GameObject>();

    // Event để thông báo khi giỏ hàng thay đổi (ví dụ: để SalesManager chính cập nhật tổng tiền)
    public event Action OnCartChanged;

    // Public getter cho giỏ hàng (để SalesFinalizeTransaction có thể truy cập)
    public Dictionary<string, ProductData> ProductsInCart => _productsInCart;


    // Phương thức khởi tạo, được gọi từ SalesManager chính
    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser, CollectionReference userProductsCollection, List<ProductData> allUserProducts, StatusPopupManager statusPopupManager)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userProductsCollection = userProductsCollection;
        _allUserProducts = allUserProducts; // Nhận tham chiếu đến allUserProducts từ SalesManager chính
        _statusPopupManager = statusPopupManager;

        // Gán listener cho các sự kiện UI của Cart Manager
        if (productSearchInputField != null) productSearchInputField.onValueChanged.AddListener(OnProductSearchRequested);
        if (closeProductSelectionPopupButton != null) closeProductSelectionPopupButton.onClick.AddListener(HideProductSelectionPopup);

        // Đảm bảo nút "Thêm sản phẩm" chính được gán listener sau khi được Instantiate
        // (logic này sẽ nằm trong UpdateCartUI)

        // Thiết lập trạng thái UI ban đầu
        if (productSelectionPopupRoot != null) productSelectionPopupRoot.SetActive(false);
        UpdateCartUI(); // Cập nhật UI giỏ hàng ban đầu (thêm nút "Thêm sản phẩm")
    }
    public ProductData GetProductFromAllUserProducts(string productId)
        {
            if (_allUserProducts == null)
            {
                Debug.LogError("SalesCartManager: _allUserProducts is null. Products list not initialized.");
                return null;
            }
            return _allUserProducts.FirstOrDefault(p => p.productId == productId);
        }


    // GỌI PHƯƠNG THỨC NÀY TỪ SALESMANAGER KHI CẦN MỞ POPUP THÊM SẢN PHẨM
    public void OnAddProductToCartMainButtonClicked()
    {
        // Kiểm tra quyền truy cập tính năng bán hàng trước (để đảm bảo không bị lỗi khi bấm)
        // Logic này có thể lặp lại nếu đã kiểm tra ở SalesManager, nhưng đảm bảo an toàn.
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null ||
            !ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales))
        {
            _statusPopupManager.ShowPopup($"Tính năng Bán hàng yêu cầu gói phù hợp. Gói hiện tại: '{currentPackageName}'. Vui lòng nâng cấp gói để sử dụng.");
            Debug.LogWarning($"SalesCartManager: Gói '{currentPackageName}' không có quyền truy cập tính năng Bán hàng.");
            return;
        }

        if (productSelectionPopupRoot != null)
        {
            productSelectionPopupRoot.SetActive(true);
            if (productSearchInputField != null)
            {
                productSearchInputField.text = "";
                productSearchInputField.ActivateInputField();
            }
            OnProductSearchRequested(""); // Load tất cả sản phẩm khi mở popup
        }
        else
        {
            Debug.LogError("SalesCartManager: productSelectionPopupRoot chưa được gán!");
            _statusPopupManager.ShowPopup("Lỗi: Không tìm thấy giao diện chọn sản phẩm.");
        }
    }

    public void HideProductSelectionPopup()
    {
        if (productSelectionPopupRoot != null)
        {
            productSelectionPopupRoot.SetActive(false);
            if (productSearchInputField != null) productSearchInputField.text = "";
            OnProductSearchRequested(""); // Clear search results
        }
    }

    private void OnProductSearchRequested(string searchText)
    {
        if (productSearchResultsContentParent == null)
        {
            Debug.LogError("SalesCartManager: productSearchResultsContentParent chưa được gán!");
            _statusPopupManager.ShowPopup("Lỗi nội bộ: Không tìm thấy nơi hiển thị kết quả sản phẩm.");
            return;
        }

        // Xóa kết quả tìm kiếm cũ
        foreach (Transform child in productSearchResultsContentParent)
        {
            Destroy(child.gameObject);
        }

        IEnumerable<ProductData> searchResults = _allUserProducts; // Sử dụng _allUserProducts được truyền vào

        if (!string.IsNullOrEmpty(searchText.Trim()))
        {
            string lowerSearchText = searchText.Trim().ToLower();
            searchResults = searchResults.Where(p =>
                (p.productName?.ToLower().Contains(lowerSearchText) ?? false) || // Sử dụng ?. và ?? false cho an toàn với null
                (p.barcode?.ToLower().Contains(lowerSearchText) ?? false)
            );
        }

        if (searchResults.Any())
        {
            foreach (ProductData product in searchResults.OrderBy(p => p.productName))
            {
                if (productSearchItemPrefab != null)
                {
                    GameObject productItemGO = Instantiate(productSearchItemPrefab, productSearchResultsContentParent);
                    ProductSearchUIItem uiItem = productItemGO.GetComponent<ProductSearchUIItem>();
                    if (uiItem != null)
                    {
                        uiItem.SetProductData(product);
                        // Gán listener cho nút "Thêm vào giỏ" của từng item tìm kiếm
                        uiItem.OnAddToCartActionRequested.AddListener(HandleAddToCartFromPopup);
                    }
                    else
                    {
                        Debug.LogWarning("Prefab productSearchItemPrefab không có script ProductSearchUIItem. Vui lòng kiểm tra lại!");
                    }
                }
                else
                {
                    Debug.LogError("SalesCartManager: productSearchItemPrefab chưa được gán!");
                    _statusPopupManager.ShowPopup("Lỗi: Prefab hiển thị sản phẩm tìm kiếm chưa được gán.");
                    break;
                }
            }
        }
        else
        {
            // Tùy chọn: Hiển thị thông báo "Không tìm thấy sản phẩm" nếu searchResults trống.
        }
    }

    private void HandleAddToCartFromPopup(ProductData productToAdd, long quantity)
    {
        // Kiểm tra quyền truy cập tính năng tồn kho (Inventory)
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        if (productToAdd == null)
        {
            Debug.LogError("SalesCartManager: productToAdd is null.");
            _statusPopupManager.ShowPopup("Lỗi: Thông tin sản phẩm không hợp lệ.");
            return;
        }

        if (string.IsNullOrEmpty(productToAdd.productId))
        {
            Debug.LogError($"SalesCartManager: productToAdd.productId is null or empty for product '{productToAdd.productName}'.");
            _statusPopupManager.ShowPopup($"Lỗi: ID sản phẩm không hợp lệ cho '{productToAdd.productName}'.");
            return;
        }

        if (_allUserProducts == null || !_allUserProducts.Any())
        {
             Debug.LogError("SalesCartManager: _allUserProducts list is unexpectedly null or empty. Data might not be synced yet.");
             _statusPopupManager.ShowPopup("Dữ liệu sản phẩm đang được tải. Vui lòng thử lại sau giây lát.");
             return;
        }

        ProductData actualInventoryProduct = _allUserProducts.FirstOrDefault(p => p.productId == productToAdd.productId);

        if (actualInventoryProduct == null)
        {
            Debug.LogError($"Sản phẩm '{productToAdd.productName}' (ID: {productToAdd.productId}) không tìm thấy trong kho cục bộ _allUserProducts. Có thể chưa đồng bộ hoặc đã bị xóa.");
            _statusPopupManager.ShowPopup($"Sản phẩm '{productToAdd.productName}' không tìm thấy trong kho. Vui lòng tải lại trang.");
            return;
        }

        long currentQuantityInCart = _productsInCart.ContainsKey(productToAdd.productId) ? _productsInCart[productToAdd.productId].stock : 0;
        long totalRequestedQuantity = currentQuantityInCart + quantity;

        // KIỂM TRA TỒN KHO CHỈ KHI CÓ TÍNH NĂNG INVENTORY
        if (hasInventoryFeature)
        {
            if (totalRequestedQuantity > actualInventoryProduct.stock)
            {
                _statusPopupManager.ShowPopup($"Không đủ hàng trong kho cho {productToAdd.productName}. Tồn kho: {actualInventoryProduct.stock}. Giỏ đã có: {currentQuantityInCart}. Yêu cầu thêm: {quantity}.");
                return;
            }
        }
        else
        {
            Debug.Log($"Gói hiện tại không quản lý tồn kho. Bỏ qua kiểm tra số lượng cho {productToAdd.productName}.");
        }

        if (_productsInCart.ContainsKey(productToAdd.productId))
        {
            _productsInCart[productToAdd.productId].stock += quantity;
        }
        else
        {
            ProductData newCartItem = new ProductData
            {
                productId = productToAdd.productId,
                productName = productToAdd.productName ?? "",
                unit = productToAdd.unit ?? "",
                price = productToAdd.price,
                importPrice = productToAdd.importPrice,
                barcode = productToAdd.barcode ?? "",
                imageUrl = productToAdd.imageUrl ?? "",
                stock = quantity,
                category = productToAdd.category ?? "",
                manufacturer = productToAdd.manufacturer ?? ""
            };
            _productsInCart.Add(newCartItem.productId, newCartItem);
        }

        UpdateCartUI(); // Cập nhật UI giỏ hàng
        OnCartChanged?.Invoke(); // Kích hoạt event để SalesManager chính cập nhật tổng tiền

        HideProductSelectionPopup();

        Debug.Log($"Đã thêm {quantity} {productToAdd.unit} {productToAdd.productName} vào giỏ hàng.");
    }

    public void UpdateCartUI()
    {
        if (cartItemsParent == null)
        {
            Debug.LogError("SalesCartManager: cartItemsParent is null. Cannot update cart UI.");
            _statusPopupManager.ShowPopup("Lỗi nội bộ: Giao diện giỏ hàng chưa được gán.");
            return;
        }
        if (cartItemPrefab == null)
        {
            Debug.LogError("SalesCartManager: cartItemPrefab is null. Cannot instantiate cart items.");
            _statusPopupManager.ShowPopup("Lỗi nội bộ: Prefab sản phẩm giỏ hàng chưa được gán.");
            return;
        }
        if (addProductToCartMainButtonPrefab == null)
        {
            Debug.LogError("SalesCartManager: addProductToCartMainButtonPrefab is null. Cannot add product button.");
            _statusPopupManager.ShowPopup("Lỗi nội bộ: Prefab nút thêm sản phẩm chưa được gán.");
            return;
        }
        if (cartAndAddProductAreaRect == null)
        {
            Debug.LogError("SalesCartManager: cartAndAddProductAreaRect chưa được gán trong Inspector!");
            _statusPopupManager.ShowPopup("Lỗi nội bộ: RectTransform của khu vực giỏ hàng chính chưa được gán.");
            return;
        }

        // Xóa tất cả các item sản phẩm cũ trong danh sách (bao gồm cả nút "Thêm sản phẩm" nếu nó có)
        foreach (Transform child in cartItemsParent.transform)
        {
            Destroy(child.gameObject);
        }
        _cartItemUIObjects.Clear();

        // Thêm các item sản phẩm hiện có trong giỏ
        foreach (var kvp in _productsInCart)
        {
            ProductData cartItemData = kvp.Value;
            GameObject cartItemGO = Instantiate(cartItemPrefab, cartItemsParent.transform);
            CartItemUI uiItem = cartItemGO.GetComponent<CartItemUI>();
            if (uiItem != null)
            {
                uiItem.SetCartItemData(cartItemData);
                // Gán listener cho các nút tăng/giảm/xóa item trong giỏ
                uiItem.OnQuantityChanged.AddListener(HandleCartItemQuantityChanged);
                uiItem.OnRemovedFromCart.AddListener(HandleRemoveCartItem);
                _cartItemUIObjects.Add(cartItemData.productId, cartItemGO);
            }
            else
            {
                Debug.LogWarning("Prefab cartItemPrefab không có script CartItemUI. Vui lòng kiểm tra lại!");
            }
        }

        // Thêm lại nút "Thêm sản phẩm" sau cùng
        GameObject addButtonGO = Instantiate(addProductToCartMainButtonPrefab, cartItemsParent.transform);
        Button addButtonComponent = addButtonGO.GetComponent<Button>();
        if (addButtonComponent != null)
        {
            // Gán listener cho nút "Thêm sản phẩm" chính
            addButtonComponent.onClick.RemoveAllListeners(); // Xóa listener cũ để tránh trùng lặp
            addButtonComponent.onClick.AddListener(OnAddProductToCartMainButtonClicked);
        }
        else
        {
            Debug.LogWarning("Prefab addProductToCartMainButtonPrefab không có component Button!");
        }

        // Cập nhật tổng tiền (thường được thực hiện bởi SalesManager chính qua OnCartChanged)
        // UpdateCartSummaryUI();

        // LOGIC ĐIỀU CHỈNH CHIỀU CAO BẰNG CODE
        int productCount = _productsInCart.Count;
        float calculatedHeight = (productCount * perProductHeight) + addButtonHeight;

        cartAndAddProductAreaRect.sizeDelta = new Vector2(cartAndAddProductAreaRect.sizeDelta.x, calculatedHeight);
        Debug.Log($"SalesCartManager: Đặt chiều cao CartAndAddProductAreaRect thành {calculatedHeight}.");
    }

    private void HandleCartItemQuantityChanged(string productId, long newQuantity)
    {
        // Kiểm tra quyền truy cập tính năng tồn kho (Inventory)
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        if (_productsInCart.ContainsKey(productId))
        {
            ProductData cartItem = _productsInCart[productId];
            ProductData inventoryProduct = _allUserProducts.FirstOrDefault(p => p.productId == productId);

            // KIỂM TRA TỒN KHO CHỈ KHI CÓ TÍNH NĂNG INVENTORY
            if (hasInventoryFeature)
            {
                if (inventoryProduct != null && newQuantity > inventoryProduct.stock)
                {
                    _statusPopupManager.ShowPopup($"Số lượng trong giỏ của {cartItem.productName} ({newQuantity}) không thể vượt quá tồn kho hiện có ({inventoryProduct.stock}).");
                    if (_cartItemUIObjects.ContainsKey(productId))
                    {
                        cartItem.stock = inventoryProduct.stock;
                        _cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem);
                    }
                    OnCartChanged?.Invoke(); // Cập nhật tổng tiền ngay cả khi bị giới hạn
                    return;
                }
            }
            else
            {
                Debug.Log($"Gói hiện tại không quản lý tồn kho. Bỏ qua kiểm tra số lượng khi thay đổi giỏ hàng cho {cartItem.productName}.");
            }

            if (newQuantity < 0) newQuantity = 0;

            cartItem.stock = newQuantity;
            if (newQuantity <= 0)
            {
                HandleRemoveCartItem(productId);
            }
            else
            {
                _cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem); // Cập nhật UI item
                OnCartChanged?.Invoke(); // Kích hoạt event để SalesManager chính cập nhật tổng tiền
            }
        }
    }

    private void HandleRemoveCartItem(string productId)
    {
        if (_productsInCart.ContainsKey(productId))
        {
            _productsInCart.Remove(productId);
            if (_cartItemUIObjects.ContainsKey(productId))
            {
                Destroy(_cartItemUIObjects[productId]);
                _cartItemUIObjects.Remove(productId);
            }
            UpdateCartUI(); // Gọi lại UpdateCartUI để làm mới và rebuild layout
            OnCartChanged?.Invoke(); // Kích hoạt event để SalesManager chính cập nhật tổng tiền
        }
    }

    // Phương thức này có thể được chuyển lên SalesManager chính hoặc FinalizeTransaction
    // vì nó liên quan đến tổng kết toàn bộ giao dịch, không chỉ giỏ hàng.
    // Tạm thời giữ lại để đảm bảo không bị lỗi trong quá trình tách.
    public void UpdateCartSummaryUI(long subtotal, long tax, long grandTotal)
    {
        // Các Text hiển thị tổng tiền có thể nằm trong SalesManager chính.
        // SalesCartManager chỉ cần thông báo OnCartChanged và SalesManager sẽ cập nhật.
        // Nếu các Text này nằm trong CartPanel, thì vẫn giữ ở đây.
    }

    // Đặt lại giỏ hàng
    public void ClearCart()
    {
        _productsInCart.Clear();
        _cartItemUIObjects.Clear();
        UpdateCartUI();
        OnCartChanged?.Invoke(); // Báo hiệu giỏ hàng đã được xóa
    }

    // Phương thức để đồng bộ danh sách sản phẩm đầy đủ (allUserProducts)
    public void SetAllUserProducts(List<ProductData> allProducts)
    {
        _allUserProducts = allProducts;
        // Nếu popup tìm kiếm đang mở, cập nhật lại kết quả tìm kiếm
        if (productSelectionPopupRoot != null && productSelectionPopupRoot.activeSelf && productSearchInputField != null)
        {
            OnProductSearchRequested(productSearchInputField.text);
        }
    }
   
}