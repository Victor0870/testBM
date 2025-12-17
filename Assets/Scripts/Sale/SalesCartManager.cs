// File: Scripts/Sale/SalesCartManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShopSessionData;

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
    private List<ProductData> _allUserProducts;
    // Sản phẩm trong giỏ hàng
    private Dictionary<string, ProductData> _productsInCart = new Dictionary<string, ProductData>();
    private Dictionary<string, GameObject> _cartItemUIObjects = new Dictionary<string, GameObject>();

    // THÊM: Biến lưu tham chiếu nút hiện tại để quản lý vòng đời
    private Button _currentAddButton;

    // Event để thông báo khi giỏ hàng thay đổi
    public event Action OnCartChanged;

    // Public getter cho giỏ hàng
    public Dictionary<string, ProductData> ProductsInCart => _productsInCart;

    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser, CollectionReference userProductsCollection, List<ProductData> allUserProducts, StatusPopupManager statusPopupManager)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userProductsCollection = userProductsCollection;
        _allUserProducts = allUserProducts;
        _statusPopupManager = statusPopupManager;

        if (productSearchInputField != null) productSearchInputField.onValueChanged.AddListener(OnProductSearchRequested);
        if (closeProductSelectionPopupButton != null) closeProductSelectionPopupButton.onClick.AddListener(HideProductSelectionPopup);

        if (productSelectionPopupRoot != null) productSelectionPopupRoot.SetActive(false);

        UpdateCartUI(); // Cập nhật UI giỏ hàng ban đầu
    }

    public ProductData GetProductFromAllUserProducts(string productId)
    {
        if (_allUserProducts == null) return null;
        return _allUserProducts.FirstOrDefault(p => p.productId == productId);
    }

    public void OnAddProductToCartMainButtonClicked()
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null ||
            !ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales))
        {
            _statusPopupManager.ShowPopup($"Tính năng Bán hàng yêu cầu gói phù hợp. Gói hiện tại: '{currentPackageName}'.");
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
            OnProductSearchRequested("");
        }
        else
        {
            _statusPopupManager.ShowPopup("Lỗi: Không tìm thấy giao diện chọn sản phẩm.");
        }
    }

    public void HideProductSelectionPopup()
    {
        if (productSelectionPopupRoot != null)
        {
            productSelectionPopupRoot.SetActive(false);
            if (productSearchInputField != null) productSearchInputField.text = "";
            OnProductSearchRequested("");
        }
    }

    private void OnProductSearchRequested(string searchText)
    {
        if (productSearchResultsContentParent == null) return;

        foreach (Transform child in productSearchResultsContentParent) Destroy(child.gameObject);

        IEnumerable<ProductData> searchResults = _allUserProducts;

        if (!string.IsNullOrEmpty(searchText.Trim()))
        {
            string lowerSearchText = searchText.Trim().ToLower();
            searchResults = searchResults.Where(p =>
                (p.productName?.ToLower().Contains(lowerSearchText) ?? false) ||
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
                        uiItem.OnAddToCartActionRequested.AddListener(HandleAddToCartFromPopup);
                    }
                }
            }
        }
    }

    private void HandleAddToCartFromPopup(ProductData productToAdd, long quantity)
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        if (productToAdd == null || string.IsNullOrEmpty(productToAdd.productId)) return;

        ProductData actualInventoryProduct = _allUserProducts.FirstOrDefault(p => p.productId == productToAdd.productId);
        if (actualInventoryProduct == null)
        {
            _statusPopupManager.ShowPopup($"Sản phẩm '{productToAdd.productName}' không tìm thấy trong kho.");
            return;
        }

        long currentQuantityInCart = _productsInCart.ContainsKey(productToAdd.productId) ? _productsInCart[productToAdd.productId].stock : 0;
        long totalRequestedQuantity = currentQuantityInCart + quantity;

        if (hasInventoryFeature)
        {
            if (totalRequestedQuantity > actualInventoryProduct.stock)
            {
                _statusPopupManager.ShowPopup($"Không đủ hàng. Tồn kho: {actualInventoryProduct.stock}. Giỏ: {currentQuantityInCart}. Thêm: {quantity}.");
                return;
            }
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

        UpdateCartUI();
        OnCartChanged?.Invoke();
        HideProductSelectionPopup();
    }

    public void UpdateCartUI()
    {
        if (cartItemsParent == null || cartItemPrefab == null || addProductToCartMainButtonPrefab == null) return;

        // 1. Xóa sạch UI cũ
        foreach (Transform child in cartItemsParent.transform)
        {
            Destroy(child.gameObject);
        }
        _cartItemUIObjects.Clear();

        // 2. Tạo lại các Item trong giỏ
        foreach (var kvp in _productsInCart)
        {
            ProductData cartItemData = kvp.Value;
            GameObject cartItemGO = Instantiate(cartItemPrefab, cartItemsParent.transform);
            CartItemUI uiItem = cartItemGO.GetComponent<CartItemUI>();
            if (uiItem != null)
            {
                uiItem.SetCartItemData(cartItemData);
                uiItem.OnQuantityChanged.AddListener(HandleCartItemQuantityChanged);
                uiItem.OnRemovedFromCart.AddListener(HandleRemoveCartItem);
                _cartItemUIObjects.Add(cartItemData.productId, cartItemGO);
            }
        }

        // 3. Tạo lại nút "Thêm sản phẩm" và GÁN LISTENER NGAY LẬP TỨC
        GameObject addButtonGO = Instantiate(addProductToCartMainButtonPrefab, cartItemsParent.transform);
        _currentAddButton = addButtonGO.GetComponent<Button>();

        if (_currentAddButton != null)
        {
            _currentAddButton.onClick.RemoveAllListeners();
            _currentAddButton.onClick.AddListener(OnAddProductToCartMainButtonClicked);
            _currentAddButton.interactable = true; // Đảm bảo nút luôn active khi mới tạo
        }

        // 4. Tính toán chiều cao
        int productCount = _productsInCart.Count;
        float calculatedHeight = (productCount * perProductHeight) + addButtonHeight;
        if (cartAndAddProductAreaRect != null)
            cartAndAddProductAreaRect.sizeDelta = new Vector2(cartAndAddProductAreaRect.sizeDelta.x, calculatedHeight);
    }

    private void HandleCartItemQuantityChanged(string productId, long newQuantity)
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        if (_productsInCart.ContainsKey(productId))
        {
            ProductData cartItem = _productsInCart[productId];
            ProductData inventoryProduct = _allUserProducts.FirstOrDefault(p => p.productId == productId);

            if (hasInventoryFeature && inventoryProduct != null && newQuantity > inventoryProduct.stock)
            {
                _statusPopupManager.ShowPopup($"Số lượng vượt quá tồn kho ({inventoryProduct.stock}).");
                if (_cartItemUIObjects.ContainsKey(productId))
                {
                    cartItem.stock = inventoryProduct.stock;
                    _cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem);
                }
                OnCartChanged?.Invoke();
                return;
            }

            if (newQuantity < 0) newQuantity = 0;
            cartItem.stock = newQuantity;

            if (newQuantity <= 0) HandleRemoveCartItem(productId);
            else
            {
                _cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem);
                OnCartChanged?.Invoke();
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
            UpdateCartUI();
            OnCartChanged?.Invoke();
        }
    }

    public void ClearCart()
    {
        _productsInCart.Clear();
        _cartItemUIObjects.Clear();
        UpdateCartUI();
        OnCartChanged?.Invoke();
    }

    public void SetAllUserProducts(List<ProductData> allProducts)
    {
        _allUserProducts = allProducts;
        if (productSelectionPopupRoot != null && productSelectionPopupRoot.activeSelf && productSearchInputField != null)
        {
            OnProductSearchRequested(productSearchInputField.text);
        }
    }
}