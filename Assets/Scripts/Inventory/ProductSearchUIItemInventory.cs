// File: ProductSearchUIItemInventory.cs

using UnityEngine;
using TMPro; // Sử dụng cho TextMeshPro
using UnityEngine.UI; // Sử dụng cho Image và Button
// Không cần UnityEngine.Events
using static ShopSessionData; // Truy cập ShopSessionData.CachedShopSettings, AppPackageConfig

public class ProductSearchUIItemInventory : MonoBehaviour
{
    [Header("Product UI Elements")]
    public TMP_Text productNameText;
    public TMP_Text unitText;
    public TMP_Text priceText;
    public TMP_Text stockText; // Hiển thị tồn kho hiện tại
    // ĐÃ XÓA: public TMP_InputField quantityInputField; // Không còn cần thiết!

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    void Awake()
    {
        // KHÔNG CÒN LOGIC THIẾT LẬP CHO InputField nào ở đây nữa.
    }

    /// <summary>
    /// Thiết lập dữ liệu sản phẩm cho dòng UI.
    /// </summary>
    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        if (productNameText != null) productNameText.text = product.productName;
        if (unitText != null) unitText.text = product.unit;
        if (priceText != null) priceText.text = $"{product.price:N0} VNĐ";

        // --- Kiểm tra quyền truy cập tính năng INVENTORY ---
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        if (stockText != null)
        {
            // Hiển thị/Ẩn Text tồn kho dựa trên tính năng
            stockText.gameObject.SetActive(hasInventoryFeature);
            if (hasInventoryFeature)
            {
                stockText.text = $"Tồn kho: {product.stock:N0}";
            }
        }

        // Kiểm tra trạng thái hết hàng nếu có tính năng tồn kho
        if (hasInventoryFeature && product.stock <= 0)
        {
            if (stockText != null) stockText.text = $"Tồn kho: {product.stock:N0} (Hết hàng)";
        }
    }

    // ĐÃ XÓA: private void OnQuantityInputEndEdit(string value)
}