// File: SalesFptInvoiceManager.cs
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth; // Cần cho FirebaseUser
using System;
using System.Collections.Generic;
using System.Linq; // Cần cho Sum
using UnityEngine.UI;
using System.Threading.Tasks;

// Using cho FPT eInvoice và SimpleJSON
using SimpleJSON;
using static ShopSessionData; // Để truy cập CachedShopSettings, AppPackageConfig
using static ShopSettingManager; // Cần cho ShopData class

public class SalesFptInvoiceManager : MonoBehaviour
{
    [Header("FPT eInvoice Integration - SalesFptInvoiceManager")]
    public Button exportInvoiceButton; // Nút Xuất hóa đơn (nếu nó không được điều khiển từ SalesManager chính)

    // Tham chiếu đến StatusPopupManager (được truyền từ SalesManager chính)
    private StatusPopupManager _statusPopupManager;
    // Tham chiếu đến Firebase (được truyền từ SalesManager chính)
    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userSalesCollection; // Để cập nhật SaleData với thông tin hóa đơn FPT
    private FptEInvoiceApiClient _fptApiClient; // Tham chiếu đến FptEInvoiceApiClient

    private const double TAX_RATE = 0.10; // Tỷ lệ thuế.

    // Phương thức khởi tạo, được gọi từ SalesManager chính
    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser, CollectionReference userSalesCollection, FptEInvoiceApiClient fptApiClient, StatusPopupManager statusPopupManager)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userSalesCollection = userSalesCollection;
        _fptApiClient = fptApiClient;
        _statusPopupManager = statusPopupManager;

        // Gán listener cho nút Xuất hóa đơn nếu nó được quản lý bởi Manager này
        if (exportInvoiceButton != null)
        {
            exportInvoiceButton.onClick.AddListener(OnExportInvoiceButtonClicked);
        }
    }

    // Phương thức này có thể được gọi từ SalesManager chính sau khi một giao dịch hoàn tất
    public async Task<(bool success, string fptInvoiceId, string fptInvoiceSeq, string fptInvoiceSerial, string fptLookupLink, string errorMessage)>
           ProcessFptInvoiceCreation(CustomerData customer, Dictionary<string, ProductData> productsInCart, SaleData savedSaleData, DocumentReference newSaleDocRef)
    {
        // Kiểm tra quyền truy cập tính năng Hóa đơn điện tử (EInvoice)
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasEInvoiceFeature = ShopSessionData.AppPackageConfig != null &&
                                  ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.EInvoice);

        // Kiểm tra cấu hình nhà cung cấp hóa đơn điện tử là FPT
        bool isFptConfigured = ShopSessionData.CachedShopSettings?.eInvoiceProvider == "FPT";

        if (!hasEInvoiceFeature)
        {
            return (false, null, null, null, null, $"Gói hiện tại: '{currentPackageName}' không có tính năng Hóa đơn điện tử. Vui lòng nâng cấp gói.");
        }
        if (!isFptConfigured)
        {
            return (false, null, null, null, null, "Bạn chưa cấu hình nhà cung cấp hóa đơn điện tử hoặc nhà cung cấp hiện tại không phải FPT. Vui lòng cấu hình trong Cài đặt Shop.");
        }

        if (_fptApiClient == null)
        {
            return (false, null, null, null, null, "Lỗi nội bộ: FPT eInvoice API Client chưa được khởi tạo.");
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            return (false, null, null, null, null, "Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
        }

        Debug.Log("SalesFptInvoiceManager: Đang tạo dữ liệu hóa đơn cho FPT eInvoice...");
        FptInvoiceData fptInvoice = CreateFptInvoiceData(customer, productsInCart);

        Debug.Log($"SalesFptInvoiceManager: Gửi yêu cầu tạo hóa đơn FPT đến URL: {_fptApiClient.fptDefaultConfig.createInvoiceUrl}");
        Debug.Log($"SalesFptInvoiceManager: FPT Invoice JSON Payload: {fptInvoice.ToJsonNode().SaveToJSON(SimpleJSON.JSONTextMode.Indent)}");

        var (fptSuccess, fptResponseData, fptErrorMessage) = await _fptApiClient.SendApiRequestAsync(
            _fptApiClient.fptDefaultConfig.createInvoiceUrl, "POST", fptInvoice.ToJsonNode());

        if (fptSuccess)
        {
            Debug.Log("SalesFptInvoiceManager: API FPT eInvoice gọi thành công. Phản hồi: " + fptResponseData);
            _statusPopupManager.ShowPopup("Đơn hàng đã hoàn tất thành công! Hóa đơn điện tử đang được xử lý.");

            try
            {
                var fptResponseJson = SimpleJSON.JSON.Parse(fptResponseData);

                SimpleJSON.JSONNode invResponseNode = fptResponseJson["inv"];

                string fptInvSid = null;
                if (invResponseNode != null && invResponseNode["sid"] != null && !invResponseNode["sid"].IsNull)
                {
                    fptInvSid = invResponseNode["sid"].Value;
                }

                string fptInvSerial = null;
                if (invResponseNode != null && invResponseNode["serial"] != null && !invResponseNode["serial"].IsNull)
                {
                    fptInvSerial = invResponseNode["serial"].Value;
                }

                string fptInvSeq = null;
                if (fptResponseJson["seq"] != null && !fptResponseJson["seq"].IsNull)
                {
                    fptInvSeq = fptResponseJson["seq"].Value;
                }

                string fptLookupLink = null;
                if (fptResponseJson["link"] != null && !fptResponseJson["link"].IsNull)
                {
                    fptLookupLink = fptResponseJson["link"].Value;
                }

                Debug.Log($"SalesFptInvoiceManager: Hóa đơn FPT đã được tạo thành công: SID: {fptInvSid ?? "N/A"}, Số: {fptInvSeq ?? "N/A"}, Ký hiệu: {fptInvSerial ?? "N/A"}, Link: {fptLookupLink ?? "N/A"}");

                // Cập nhật SaleData trong Firestore với thông tin hóa đơn FPT
                if (newSaleDocRef != null && !string.IsNullOrEmpty(newSaleDocRef.Id))
                {
                    Dictionary<string, object> fptInvoiceUpdates = new Dictionary<string, object>
                    {
                        {"fptInvoiceId", fptInvSid},
                        {"fptInvoiceSeq", fptInvSeq},
                        {"fptInvoiceSerial", fptInvSerial},
                        {"fptLookupLink", fptLookupLink}
                    };
                    await newSaleDocRef.UpdateAsync(fptInvoiceUpdates);
                    Debug.Log("SalesFptInvoiceManager: Đã cập nhật thông tin hóa đơn FPT vào SaleData Firestore.");
                }
                else
                {
                    Debug.LogWarning("SalesFptInvoiceManager: Không thể cập nhật thông tin hóa đơn FPT vào SaleData vì newSaleDocRef.Id bị rỗng.");
                }
                return (true, fptInvSid, fptInvSeq, fptInvSerial, fptLookupLink, null); // Trả về thông tin hóa đơn FPT
            }
            catch (Exception jsonEx)
            {
                Debug.LogError($"SalesFptInvoiceManager: Lỗi khi phân tích phản hồi thành công từ FPT API: {jsonEx.Message}. Response: {fptResponseData}");
                return (false, null, null, null, null, "Lỗi khi đọc phản hồi hóa đơn FPT: " + jsonEx.Message);
            }
        }
        else
        {
            Debug.LogError($"SalesFptInvoiceManager: Lỗi khi gọi API FPT eInvoice: {fptErrorMessage}");
            return (false, null, null, null, null, fptErrorMessage); // Trả về lỗi từ FPT API
        }
    }


    // Phương thức xử lý khi nút "Xuất hóa đơn" được bấm (nếu nó không phải là phần của FinalizeTransaction)
    public void OnExportInvoiceButtonClicked()
    {
        // Kiểm tra quyền truy cập tính năng Hóa đơn điện tử trước
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null ||
            !ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.EInvoice))
        {
            _statusPopupManager.ShowPopup($"Tính năng 'Xuất hóa đơn' yêu cầu gói phù hợp. Gói hiện tại: '{currentPackageName}'. Vui lòng nâng cấp gói để sử dụng.");
            Debug.LogWarning($"SalesFptInvoiceManager: Gói '{currentPackageName}' không có quyền truy cập tính năng Hóa đơn điện tử.");
            return;
        }

        Debug.Log("SalesFptInvoiceManager: Nút 'Xuất hóa đơn' được nhấn. Chức năng sẽ được phát triển sau.");
        _statusPopupManager.ShowPopup("Chức năng 'Xuất hóa đơn' đang được phát triển.");
    }


    // Phương thức chuyển đổi ProductData của Bizmate thành FptInvoiceItem
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
            line = lineNumber,
            name = product.productName ?? "",
            unit = product.unit ?? "",
            quantity = itemQuantity,
            price = itemPrice,
            amount = itemAmount,
            vat = itemVat,
            total = itemTotal,

            code = product.barcode ?? "",
            vrt = "10",
        };
        return fptItem;
    }

    // Phương thức tạo đối tượng FptInvoiceData từ dữ liệu Sale của Bizmate
    private FptInvoiceData CreateFptInvoiceData(CustomerData customer, Dictionary<string, ProductData> productsInCart)
    {
        FptInvoiceData fptInvoice = new FptInvoiceData();

        ShopData currentShopSettings = ShopSessionData.CachedShopSettings;

        fptInvoice.inv.type = currentShopSettings.invoiceType ?? "";
        fptInvoice.inv.form = currentShopSettings.invoiceForm ?? "";
        fptInvoice.inv.serial = currentShopSettings.invoiceSerial ?? "";

        fptInvoice.inv.aun = 2;

        fptInvoice.inv.idt = DateTime.UtcNow.AddHours(7).AddSeconds(-5).ToString("yyyy-MM-dd HH:mm:ss");

        fptInvoice.inv.sid = Guid.NewGuid().ToString();

        fptInvoice.inv.paym = "TM";
        fptInvoice.inv.note = null;

        fptInvoice.inv.stax = currentShopSettings.taxId ?? "";

        if (customer != null)
        {
            fptInvoice.inv.btax = customer.taxId ?? "";
            fptInvoice.inv.baddr = customer.address ?? "";
            fptInvoice.inv.btel = customer.phone ?? "";

            if (customer.customerType == "Công ty")
            {
                fptInvoice.inv.bname = customer.companyName ?? "";
                fptInvoice.inv.buyer = customer.name ?? "";
            }
            else
            {
                fptInvoice.inv.bname = "Khách lẻ";
                fptInvoice.inv.buyer = customer.name ?? "";
            }

            if (!string.IsNullOrEmpty(customer.idNumber))
            {
                fptInvoice.inv.idnumber = customer.idNumber ?? "";
            }
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