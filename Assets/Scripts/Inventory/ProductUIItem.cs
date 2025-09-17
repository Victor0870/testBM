using UnityEngine;
using TMPro; // Sử dụng cho TextMeshPro
using UnityEngine.UI; // Sử dụng cho Image và Button
using System.Collections; // Dành cho Coroutine nếu tải ảnh
using UnityEngine.Events; // Quan trọng: Thêm namespace này cho UnityEvent

public class ProductUIItem : MonoBehaviour
{
    [Header("Product UI Elements")]
    public TMP_Text productNameText;
    public TMP_Text unitText;
    public TMP_Text priceText;
    public TMP_Text categoryText;
    public TMP_Text manufacturerText; // <-- THÊM DÒNG NÀY: Trường Text cho nhà sản xuất
    public Image productImage; // Kéo component Image vào đây (tùy chọn)

    // Các TMP_Text khác nếu cần:
    // public TMP_Text barcodeText;
    // public TMP_Text importPriceText;

    public TMP_Text stockText; // Để hiển thị thông tin tồn kho

    // Thêm Button Edit
    [Header("Interaction Buttons")]
    public Button editButton; // Kéo Button component của nút Edit vào đây trong Inspector
    public Button importStockButton; // Nút Nhập kho mới

    // Định nghĩa một UnityEvent để gửi dữ liệu sản phẩm khi nút Edit được nhấn
    // InventoryManager sẽ đăng ký lắng nghe sự kiện này
    public UnityEvent<ProductData> OnEditActionRequested;
    public UnityEvent<ProductData> OnImportStockRequested; // Sự kiện cho nút Nhập kho

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    void Awake()
    {
        // Khởi tạo UnityEvent nếu nó chưa được khởi tạo
        if (OnEditActionRequested == null)
        {
            OnEditActionRequested = new UnityEvent<ProductData>();
        }
        if (OnImportStockRequested == null)
        {
            OnImportStockRequested = new UnityEvent<ProductData>();
        }

        // Gán hàm xử lý sự kiện cho nút Edit
        if (editButton != null)
        {
            editButton.onClick.AddListener(OnEditButtonClicked);
        }
        // Gán hàm xử lý sự kiện cho nút Nhập kho
        if (importStockButton != null)
        {
            importStockButton.onClick.AddListener(OnImportStockButtonClicked);
        }
    }

    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        if (productNameText != null) productNameText.text = product.productName;
        if (unitText != null) unitText.text = product.unit;
        if (priceText != null) priceText.text = $"{product.price:N0} VNĐ";
        if (categoryText != null) categoryText.text = product.category;
        if (manufacturerText != null) manufacturerText.text = product.manufacturer; // <-- THÊM DÒNG NÀY: Cập nhật Text nhà sản xuất

        if (stockText != null) stockText.text = $" {product.stock:N0}"; // số tồn kho

        // Logic tải ảnh từ URL (nếu có và bạn muốn giữ)
        // if (productImage != null && !string.IsNullOrEmpty(product.imageUrl))
        // {
        //     StartCoroutine(LoadImage(product.imageUrl));
        // }
        // else if (productImage != null)
        // {
        //     productImage.sprite = null; // Hoặc một sprite mặc định
        // }

        // Cập nhật các text khác nếu có
        // if (barcodeText != null) barcodeText.text = product.barcode;
        // ...
    }

    // Phương thức này sẽ được gọi khi nút Edit trên item UI được nhấn
    public void OnEditButtonClicked()
    {
        Debug.Log($"Nút chỉnh sửa đã được nhấn cho sản phẩm: {currentProductData.productName}");
        // Kích hoạt sự kiện và truyền dữ liệu sản phẩm hiện tại
        OnEditActionRequested?.Invoke(currentProductData);
    }

    // Xử lý khi nút Nhập kho được nhấn
    public void OnImportStockButtonClicked()
    {
        Debug.Log($"Nút nhập kho đã được nhấn cho sản phẩm: {currentProductData.productName}");
        // Kích hoạt sự kiện và truyền dữ liệu sản phẩm hiện tại
        OnImportStockRequested?.Invoke(currentProductData);
    }

    // Tùy chọn: Hàm tải ảnh từ URL (cần using UnityEngine.Networking;)
    // private IEnumerator LoadImage(string url)
    // {
    //     // ... code tải ảnh ...
    // }
}