using UnityEngine;
using TMPro; // Sử dụng cho TextMeshPro
using UnityEngine.UI; // Sử dụng cho Image và Button
using UnityEngine.Events; // Quan trọng: Thêm namespace này cho UnityEvent

// THÊM using cho ShopSessionData và AppFeature
using static ShopSessionData;
// using static AuthManager; // Để truy cập AuthManager.GlobalAppConfig nếu cần, hoặc truy cập qua ShopSessionData.GlobalAppConfig

public class ProductSearchUIItem : MonoBehaviour
{
    [Header("Product UI Elements")]
    public TMP_Text productNameText;
    public TMP_Text unitText;
    public TMP_Text priceText;
    public TMP_Text stockText; // Hiển thị tồn kho hiện tại
    public TMP_InputField quantityInputField; // InputField cho số lượng muốn thêm vào giỏ

    [Header("Interaction Buttons")]
    public Button addToCartButton; // Nút "Thêm vào giỏ" cho item này

    // Định nghĩa một UnityEvent để gửi dữ liệu sản phẩm và số lượng khi nút được nhấn
    // SalesCartManager sẽ đăng ký lắng nghe sự kiện này
    public UnityEvent<ProductData, long> OnAddToCartActionRequested;

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    void Awake()
    {
        if (OnAddToCartActionRequested == null)
        {
            OnAddToCartActionRequested = new UnityEvent<ProductData, long>();
        }

        if (addToCartButton != null)
        {
            addToCartButton.onClick.RemoveAllListeners();
            addToCartButton.onClick.AddListener(OnAddToCartButtonClicked);
        }
        else
        {
            Debug.LogWarning("ProductSearchUIItem: Nút AddToCartButton chưa được gán!");
        }

        if (quantityInputField != null)
        {
            quantityInputField.text = "1";
            quantityInputField.onEndEdit.AddListener(OnQuantityInputEndEdit);
        }
    }

    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        if (productNameText != null) productNameText.text = product.productName;
        if (unitText != null) unitText.text = product.unit;
        if (priceText != null) priceText.text = $"{product.price:N0} VNĐ";

        // --- THAY ĐỔI TẠI ĐÂY: ẨN THÔNG TIN TỒN KHO NẾU KHÔNG CÓ TÍNH NĂNG INVENTORY ---
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(ShopSessionData.CachedShopSettings?.packageType, AppFeature.Inventory); //

        if (stockText != null)
        {
            stockText.gameObject.SetActive(hasInventoryFeature); // Ẩn/hiện Text tồn kho
            if (hasInventoryFeature)
            {
                stockText.text = $"Tồn kho: {product.stock:N0}";
            }
            else
            {
                stockText.text = ""; // Xóa text nếu bị ẩn (hoặc để trống)
            }
        }
        
        // Cũng có thể ẩn/hiện InputField số lượng hoặc điều chỉnh giới hạn của nó
        // Tùy theo logic bạn muốn: nếu không quản lý kho, có thể bán số lượng bất kỳ
        if (quantityInputField != null)
        {
            // quantityInputField.interactable = hasInventoryFeature; // Nếu muốn vô hiệu hóa nhập số lượng khi không quản lý kho
            // Hoặc giới hạn max quantity theo tồn kho chỉ khi có feature
            if (!hasInventoryFeature) {
                // Nếu không có tính năng tồn kho, có thể bỏ qua giới hạn số lượng nhập
                quantityInputField.text = "1"; // Reset về 1 nếu thay đổi trạng thái
                quantityInputField.targetGraphic.color = Color.white; // Đảm bảo màu không đỏ
            }
        }


        if (addToCartButton != null)
        {
            // Nút AddToCart vẫn tương tác nếu có sản phẩm, nhưng logic kiểm tra tồn kho sẽ ở SalesCartManager.
            // Nếu có tính năng tồn kho, thì chỉ cho bấm khi stock > 0
            // Nếu không có tính năng tồn kho, thì luôn cho bấm (coi như luôn có hàng)
            addToCartButton.interactable = hasInventoryFeature ? (product.stock > 0) : true;
            
            // Nếu hết hàng và có tính năng tồn kho, hiển thị (Hết hàng)
            if (hasInventoryFeature && product.stock <= 0)
            {
                if (stockText != null) stockText.text = $"Tồn kho: {product.stock:N0} (Hết hàng)";
            }
        }
    }

    private void OnQuantityInputEndEdit(string value)
    {
        if (quantityInputField == null) return;

        long quantity;
        if (!long.TryParse(value, out quantity) || quantity <= 0)
        {
            Debug.LogWarning($"ProductSearchUIItem: Số lượng không hợp lệ cho {currentProductData.productName}: {value}");
            if (quantityInputField.targetGraphic != null)
            {
                quantityInputField.targetGraphic.color = Color.red;
            }
            quantityInputField.text = "1";
            return;
        }

        // KIỂM TRA TỒN KHO CHỈ KHI CÓ TÍNH NĂNG INVENTORY (Giống HandleAddToCartFromPopup)
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(ShopSessionData.CachedShopSettings?.packageType, AppFeature.Inventory);

        if (hasInventoryFeature)
        {
            if (quantity > currentProductData.stock)
            {
                Debug.LogWarning($"ProductSearchUIItem: Số lượng nhập vượt quá tồn kho cho {currentProductData.productName}. Tồn kho: {currentProductData.stock}");
                if (quantityInputField.targetGraphic != null)
                {
                    quantityInputField.targetGraphic.color = Color.red;
                }
                quantityInputField.text = currentProductData.stock.ToString();
                return;
            }
        }

        if (quantityInputField.targetGraphic != null)
        {
            quantityInputField.targetGraphic.color = Color.white;
        }
    }


    public void OnAddToCartButtonClicked()
    {
        if (currentProductData == null)
        {
            Debug.LogError("ProductSearchUIItem: currentProductData is null. Không thể thêm vào giỏ.");
            return;
        }

        long quantity;
        if (quantityInputField == null || !long.TryParse(quantityInputField.text, out quantity) || quantity <= 0)
        {
            Debug.LogError($"ProductSearchUIItem: Số lượng không hợp lệ cho sản phẩm {currentProductData.productName}.");
            return;
        }

        OnAddToCartActionRequested?.Invoke(currentProductData, quantity);
    }
}