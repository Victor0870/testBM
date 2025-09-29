// File: Scripts/Sale/SalesFptInvoiceManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Auth;
using daBizmate;
using static ShopSessionData;
using static ShopSettingManager;
using UnityEngine.UI; // <-- SỬA LỖI CS0246: Thêm using cho Button
using System.Linq;

public class SalesFptInvoiceManager : MonoBehaviour
{
    // --- KHAI BÁO THÀNH PHẦN MỚI ---
    [Header("Dependencies")]
    public FptEInvoiceApiClient apiClient;
    public StatusPopupManager statusPopupManager;

    // --- BỔ SUNG ĐỂ SỬA LỖI CS1061 ---
    [Header("UI Control")]
    public Button exportInvoiceButton; // Re-added field

    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userSalesCollection;

    // Configs từ ShopSettingManager (Đã bỏ các trường Button)
    private string fptAccount;
    private string fptPassword;
    private string invoiceSerial;
    private string invoiceForm;
    private string invoiceType;
    private string taxId;

    private const double TAX_RATE = 0.10; // Cần cho hàm ánh xạ tạm thời

    /// <summary>
    /// Phương thức khởi tạo mới, chỉ nhận ShopData thay vì các tham chiếu cũ
    /// </summary>
    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser, CollectionReference userSalesCollection, ShopData shopData)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userSalesCollection = userSalesCollection;

        // Tải các thông tin cấu hình HĐĐT từ ShopData
        fptAccount = shopData.eInvoiceUser;
        fptPassword = shopData.eInvoicePass;
        invoiceSerial = shopData.invoiceSerial;
        invoiceForm = shopData.invoiceForm;
        invoiceType = shopData.invoiceType;
        taxId = shopData.taxId;

        Debug.Log("SalesFptInvoiceManager đã được khởi tạo.");
    }

    /// <summary>
    /// Phương thức cốt lõi xử lý việc tạo và phát hành hóa đơn FPT.
    /// </summary>
    public async Task<(bool success, string fptInvId, string fptInvSeq, string fptInvSerial, string fptLookupLink, string fptErrorMsg)>
    ProcessFptInvoiceCreation(CustomerData customer, Dictionary<string, ProductData> productsInCart, SaleData saleData, DocumentReference saleDocRef)
    {
        // 1. KIỂM TRA ĐIỀU KIỆN TIÊN QUYẾT
        if (saleData == null || _currentUser == null || apiClient == null || statusPopupManager == null)
        {
            return (false, null, null, null, null, "Hệ thống chưa được khởi tạo đầy đủ.");
        }

        if (!AppPackageConfig.HasFeature(CachedShopSettings?.packageType, AppFeature.EInvoice))
        {
            return (false, null, null, null, null, "Gói hiện tại không hỗ trợ tính năng Hóa đơn điện tử.");
        }

        if (string.IsNullOrEmpty(fptAccount) || string.IsNullOrEmpty(fptPassword) || string.IsNullOrEmpty(invoiceSerial))
        {
            return (false, null, null, null, null, "Cấu hình Hóa đơn điện tử (Tài khoản/Mật khẩu/Serial) chưa được thiết lập.");
        }

        // 2. TÌM THAM CHIẾU DOCUMENT
        if (saleDocRef == null && !string.IsNullOrEmpty(saleData.saleId))
        {
            if (_userSalesCollection == null)
                return (false, null, null, null, null, "Bộ sưu tập Sales chưa được khởi tạo.");

            saleDocRef = _userSalesCollection.Document(saleData.saleId);
            if (saleDocRef == null)
            {
                return (false, null, null, null, null, $"Không tìm thấy DocumentReference cho SaleId: {saleData.saleId}");
            }
        }
        else if (saleDocRef == null)
        {
            return (false, null, null, null, null, "Không tìm thấy ID đơn hàng để cập nhật thông tin HĐĐT.");
        }

        // 3. TẠO DỮ LIỆU HÓA ĐƠN FPT (MAPPING)
        FptInvoiceData fptData;
        try
        {
            // THAY THẾ CHO LỖI CS0117: TẠO HÀM MAPPING TẠM THỜI
            fptData = CreateFptInvoiceData(customer, productsInCart);
        }
        catch (Exception e)
        {
            return (false, null, null, null, null, $"Lỗi ánh xạ dữ liệu HĐĐT: {e.Message}");
        }

        statusPopupManager.ShowPopup("Đang gửi yêu cầu tạo Hóa đơn điện tử FPT. Vui lòng chờ...");

        // 4. GỌI API TẠO HÓA ĐƠN FPT
        FptEInvoiceApiClient.FptInvoiceResult result;
        try
        {
            result = await apiClient.CreateInvoice(fptData);
        }
        catch (Exception e)
        {
            return (false, null, null, null, null, $"Lỗi kết nối API FPT: {e.Message}");
        }

        // 5. PHÂN TÍCH KẾT QUẢ VÀ CẬP NHẬT FIRESTORE
        if (result.Success)
        {
            // Cập nhật SaleData cục bộ trước
            saleData.fptInvoiceId = result.InvoiceId;
            saleData.fptInvoiceSeq = result.InvoiceSeq;
            saleData.fptInvoiceSerial = result.InvoiceSerial;
            saleData.fptLookupLink = result.LookupLink;

            // Chuẩn bị cập nhật Firestore
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                {"fptInvoiceId", result.InvoiceId},
                {"fptInvoiceSeq", result.InvoiceSeq},
                {"fptInvoiceSerial", result.InvoiceSerial},
                {"fptLookupLink", result.LookupLink}
            };

            // LƯU CẬP NHẬT VÀO FIRESTORE
            try
            {
                await saleDocRef.UpdateAsync(updates);
                Debug.Log($"SalesFptInvoiceManager: Đã cập nhật Firestore với ID hóa đơn FPT: {result.InvoiceId}");
                statusPopupManager.ShowPopup("Tạo Hóa đơn điện tử thành công!");

                return (true, result.InvoiceId, result.InvoiceSeq, result.InvoiceSerial, result.LookupLink, null);
            }
            catch (Exception e)
            {
                string error = $"Tạo HĐĐT thành công, NHƯNG LỖI cập nhật Firestore: {e.Message}. Vui lòng liên hệ hỗ trợ. Hóa đơn ID: {result.InvoiceId}";
                Debug.LogError(error);
                statusPopupManager.ShowPopup(error);

                return (true, result.InvoiceId, result.InvoiceSeq, result.InvoiceSerial, result.LookupLink, error);
            }
        }
        else
        {
            // API FPT trả về lỗi
            string errorMsg = $"Lỗi HĐĐT: {result.ErrorCode} - {result.ErrorMessage}. Chi tiết: {result.DetailMessage}";
            Debug.LogError(errorMsg);
            statusPopupManager.ShowPopup(errorMsg);

            return (false, null, null, null, null, errorMsg);
        }
    }

    // --- PHƯƠNG THỨC BỔ SUNG ĐỂ SỬA LỖI CS1061 ---
    public void OnExportInvoiceButtonClicked()
    {
        Debug.Log("Export Invoice button clicked. Logic should be triggered from SalesFinalizeTransaction or SaleOrderDetailPanel.");
        statusPopupManager.ShowPopup("Xuất hóa đơn điện tử sẽ được xử lý tự động trong quy trình thanh toán.");
    }

    // --- HÀM MAPPING TẠM THỜI THAY THẾ FptInvoiceData.CreateFromSale ---
    private FptInvoiceItem ConvertProductToFptInvoiceItem(ProductData product, long quantity, int lineNumber)
    {
        double itemPrice = (double)product.price;
        double itemQuantity = (double)quantity;

        double itemAmount = itemPrice * itemQuantity;
        double itemVatRate = TAX_RATE;
        double itemVat = itemAmount * itemVatRate;
        double itemTotal = itemAmount + itemVat;

        FptInvoiceItem fptItem = new FptInvoiceItem
        {
            // Chỉ điền các trường cần thiết cho Ánh xạ đơn giản
            line = lineNumber,
            name = product.productName ?? "",
            unit = product.unit ?? "",
            quantity = itemQuantity,
            price = itemPrice,
            amount = itemAmount,
            vat = itemVat,
            total = itemTotal,
            code = product.barcode ?? "",
            vrt = "10", // Giả định
        };
        return fptItem;
    }

    private FptInvoiceData CreateFptInvoiceData(CustomerData customer, Dictionary<string, ProductData> productsInCart)
    {
        FptInvoiceData fptInvoice = new FptInvoiceData();

        fptInvoice.inv.type = invoiceType ?? "";
        fptInvoice.inv.form = invoiceForm ?? "";
        fptInvoice.inv.serial = invoiceSerial ?? "";
        fptInvoice.inv.aun = 2;
        fptInvoice.inv.idt = DateTime.UtcNow.AddHours(7).AddSeconds(-5).ToString("yyyy-MM-dd HH:mm:ss");
        fptInvoice.inv.sid = Guid.NewGuid().ToString();
        fptInvoice.inv.paym = "TM";
        fptInvoice.inv.stax = taxId ?? "";

        if (customer != null)
        {
            fptInvoice.inv.btax = customer.taxId ?? "";
            fptInvoice.inv.baddr = customer.address ?? "";
            if (customer.customerType == "Công ty") fptInvoice.inv.bname = customer.companyName ?? "";
            else fptInvoice.inv.bname = "Khách lẻ";
            fptInvoice.inv.buyer = customer.name ?? "";
        }

        int lineNumber = 1;
        foreach (var kvp in productsInCart)
        {
            ProductData product = kvp.Value;
            fptInvoice.inv.items.Add(ConvertProductToFptInvoiceItem(product, product.stock, lineNumber++));
        }

        double subtotalDouble = (double)productsInCart.Values.Sum(p => p.price * p.stock);
        double taxAmountDouble = subtotalDouble * TAX_RATE;
        double totalAmountDouble = subtotalDouble + taxAmountDouble;

        fptInvoice.inv.sum = subtotalDouble;
        fptInvoice.inv.vat = taxAmountDouble;
        fptInvoice.inv.total = totalAmountDouble;
        fptInvoice.inv.sumv = subtotalDouble;
        fptInvoice.inv.vatv = taxAmountDouble;
        fptInvoice.inv.totalv = totalAmountDouble;

        return fptInvoice;
    }
}