// File: CartItemUI.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;

// THÊM using cho ShopSessionData và AppFeature
using static ShopSessionData;
// using static AuthManager; // Để truy cập AuthManager.GlobalAppConfig nếu cần, hoặc truy cập qua ShopSessionData.GlobalAppConfig

public class CartItemUI : MonoBehaviour
{
    public TMP_Text productNameText;
    public TMP_Text manufacturerText;
    public TMP_Text quantityText;
    public TMP_Text priceText;
    public TMP_Text subtotalText;

    public Button increaseQuantityButton;
    public Button decreaseQuantityButton;
    public Button removeButton;

    // Events để thông báo cho SalesManager (hoặc SalesCartManager) khi có thay đổi
    public UnityEvent<string, long> OnQuantityChanged = new UnityEvent<string, long>();
    public UnityEvent<string> OnRemovedFromCart = new UnityEvent<string>();

    private ProductData _cartItemData;

    public void SetCartItemData(ProductData data)
    {
        _cartItemData = data;

        if (productNameText != null) productNameText.text = data.productName;
        if (manufacturerText != null) manufacturerText.text = "Nhà sản xuất: " + (data.manufacturer ?? "N/A"); // Đảm bảo không null
        if (quantityText != null) quantityText.text = data.stock.ToString();
        if (priceText != null) priceText.text = $"{data.price:N0} VNĐ";
        if (subtotalText != null) subtotalText.text = $"{data.price * data.stock:N0} VNĐ";

        if (increaseQuantityButton != null)
        {
            increaseQuantityButton.onClick.RemoveAllListeners();
            increaseQuantityButton.onClick.AddListener(OnIncreaseQuantity);
        }
        if (decreaseQuantityButton != null)
        {
            decreaseQuantityButton.onClick.RemoveAllListeners();
            decreaseQuantityButton.onClick.AddListener(OnDecreaseQuantity);
        }
        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(OnRemoveItem);
        }
    }

    private void OnIncreaseQuantity()
    {
        // Kiểm tra quyền truy cập tính năng tồn kho (Inventory)
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory); //
        
        // Cần tham chiếu đến danh sách sản phẩm đầy đủ để kiểm tra tồn kho tối đa.
        // CartItemUI không có _allUserProducts, nên logic kiểm tra số lượng vượt quá tồn kho
        // đã được di chuyển và xử lý trong SalesCartManager.HandleCartItemQuantityChanged.
        // CartItemUI chỉ cần tăng số lượng và thông báo sự kiện.
        
        _cartItemData.stock++;
        quantityText.text = _cartItemData.stock.ToString();
        subtotalText.text = $"{_cartItemData.price * _cartItemData.stock:N0} VNĐ";
        OnQuantityChanged.Invoke(_cartItemData.productId, _cartItemData.stock);
    }

    private void OnDecreaseQuantity()
    {
        // Kiểm tra quyền truy cập tính năng tồn kho (Inventory)
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory); //

        _cartItemData.stock--;
        if (_cartItemData.stock <= 0)
        {
            OnRemovedFromCart.Invoke(_cartItemData.productId); // Tự động xóa nếu số lượng về 0
        }
        else
        {
            // Logic kiểm tra số lượng vượt quá tồn kho (nếu có tính năng Inventory)
            // cũng được xử lý trong SalesCartManager.HandleCartItemQuantityChanged.
            // CartItemUI chỉ cần giảm số lượng và thông báo sự kiện.
            
            quantityText.text = _cartItemData.stock.ToString();
            subtotalText.text = $"{_cartItemData.price * _cartItemData.stock:N0} VNĐ";
            OnQuantityChanged.Invoke(_cartItemData.productId, _cartItemData.stock);
        }
    }

    private void OnRemoveItem()
    {
        OnRemovedFromCart.Invoke(_cartItemData.productId);
    }
}