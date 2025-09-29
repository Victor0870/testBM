// File: Scripts/Sale/SalesFinalizeTransaction.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq; // Cần cho Sum
using System.Threading.Tasks; // Cần cho async/await

// Đảm bảo các using cần thiết từ SalesManager được đưa vào đây
using static ShopSessionData; // Để truy cập CachedShopSettings, AppPackageConfig
using static ShopSettingManager; // Cần cho ShopData class
// ĐÃ XÓA: using SalesDataService; // CS0138
// THÊM USING EXPLICIT CHO SALESDATASERVICE CLASS NẾU NÓ Ở ROOT NAMESPACE

public class SalesFinalizeTransaction : MonoBehaviour
{
    [Header("Payment Summary UI - SalesFinalizeTransaction")]
    public TMP_Text subtotalText;
    public TMP_Text taxText;
    public TMP_Text grandTotalText;
    public Button completeSaleButton;
    public Button cancelSaleButton;
    public Button exportInvoiceButton; // Nút xuất hóa đơn (nếu được quản lý ở đây)

    // Tham chiếu đến StatusPopupManager (được truyền từ SalesManager chính)
    private StatusPopupManager _statusPopupManager;
    // Tham chiếu đến các Manager con và Firebase (được truyền từ SalesManager chính)
    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userSalesCollection;
    private CollectionReference _userProductsCollection; // Để trừ kho

    private SalesCustomerManager _customerManager; // Để lấy CustomerData và cập nhật
    private SalesCartManager _cartManager;         // Để lấy ProductsInCart và xóa giỏ
    private SalesFptInvoiceManager _fptInvoiceManager; // Để xử lý hóa đơn FPT

    private TMP_Text _customerLookupStatusText; // Để cập nhật trạng thái tra cứu khách hàng
    private bool listenersInitialized = false;

    private const double TAX_RATE = 0.10;

    // Phương thức khởi tạo, được gọi từ SalesManager chính
    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser,
                           CollectionReference userSalesCollection, CollectionReference userProductsCollection,
                           SalesCustomerManager customerManager, SalesCartManager cartManager,
                           SalesFptInvoiceManager fptInvoiceManager, StatusPopupManager statusPopupManager,
                           TMP_Text customerLookupStatusTextRef) // Thêm customerLookupStatusTextRef
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userSalesCollection = userSalesCollection;
        _userProductsCollection = userProductsCollection;
        _customerManager = customerManager;
        _cartManager = cartManager;
        _fptInvoiceManager = fptInvoiceManager;
        _statusPopupManager = statusPopupManager;
        _customerLookupStatusText = customerLookupStatusTextRef; // Gán tham chiếu

        // Gán listener cho các nút
        if (!listenersInitialized)
        {
            if (completeSaleButton != null) completeSaleButton.onClick.AddListener(OnCompleteSaleButtonClicked);
            if (cancelSaleButton != null) cancelSaleButton.onClick.AddListener(OnCancelSaleButtonClicked);
            if (exportInvoiceButton != null) exportInvoiceButton.onClick.AddListener(OnExportInvoiceButtonClicked);
        }
        // Đăng ký lắng nghe sự kiện thay đổi giỏ hàng từ SalesCartManager
        if (_cartManager != null)
        {
            _cartManager.OnCartChanged += UpdateCartSummaryUI;
        }

        // Cập nhật UI tổng tiền ban đầu (giỏ hàng có thể đã có từ trước)
        UpdateCartSummaryUI();
    }

    // Cập nhật UI tổng kết giỏ hàng (được gọi khi giỏ hàng thay đổi)
    public void UpdateCartSummaryUI()
    {
        long currentSubtotal = 0;
        if (_cartManager != null && _cartManager.ProductsInCart != null)
        {
            foreach (var product in _cartManager.ProductsInCart.Values)
            {
                currentSubtotal += product.price * product.stock;
            }
        }

        long currentTax = (long)(currentSubtotal * TAX_RATE);
        long currentGrandTotal = currentSubtotal + currentTax;

        if (subtotalText != null) subtotalText.text = $"  {currentSubtotal:N0} VNĐ";
        if (taxText != null) taxText.text = $" ({TAX_RATE * 100}%): {currentTax:N0} VNĐ";
        if (grandTotalText != null) grandTotalText.text = $"  {currentGrandTotal:N0} VNĐ";

        // Kích hoạt/vô hiệu hóa nút hoàn tất đơn hàng dựa trên số lượng sản phẩm trong giỏ
        if (completeSaleButton != null)
        {
            completeSaleButton.interactable = _cartManager != null && _cartManager.ProductsInCart != null && _cartManager.ProductsInCart.Count > 0;
        }
    }


    public async void OnCompleteSaleButtonClicked()
    {
        // --- 1. Kiểm tra điều kiện ban đầu ---

        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasSalesFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales);
        // Kiểm tra quyền truy cập tồn kho và Cloud Sync
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        bool isCloudSyncEnabled = SalesDataService.Instance != null && SalesDataService.Instance.IsCloudSyncEnabled; // Sửa lỗi tiền tố

        if (_cartManager == null || _cartManager.ProductsInCart == null || _cartManager.ProductsInCart.Count == 0)
        {
            _statusPopupManager.ShowPopup("Giỏ hàng trống. Không thể hoàn tất đơn hàng.");
            return;
        }
        // Kiểm tra người dùng Firebase chỉ khi cần Cloud Sync
        if (isCloudSyncEnabled && _currentUser == null)
        {
            _statusPopupManager.ShowPopup("Lỗi: Người dùng chưa đăng nhập. Vui lòng đăng nhập lại.");
            return;
        }
        if (ShopSessionData.CachedShopSettings == null)
        {
            _statusPopupManager.ShowPopup("Lỗi: Thông tin shop chưa được tải. Vui lòng khởi động lại ứng dụng hoặc đăng nhập lại.");
            return;
        }

        if (!hasSalesFeature)
        {
            _statusPopupManager.ShowPopup($"Tính năng Bán hàng yêu cầu gói phù hợp. Gói hiện tại: '{currentPackageName}'. Vui lòng nâng cấp gói để sử dụng.");
            Debug.LogWarning($"SalesFinalizeTransaction: Gói '{currentPackageName}' không có quyền truy cập tính năng Bán hàng.");
            SetButtonsInteractable(true);
            return;
        }

        // --- 2. Lấy và Xác thực thông tin khách hàng ---
        CustomerData finalCustomerDataFromUI = _customerManager.GetCustomerDataFromUI(); // Lấy data từ UI

        if (string.IsNullOrEmpty(finalCustomerDataFromUI.name))
        {
            finalCustomerDataFromUI.name = "Khách lẻ";
            Debug.Log("Tên khách hàng trống, đã đặt mặc định là 'Khách lẻ'.");
        }

        if (finalCustomerDataFromUI.customerType == "Công ty" && string.IsNullOrEmpty(finalCustomerDataFromUI.companyName))
        {
            _statusPopupManager.ShowPopup("Vui lòng nhập Tên công ty khi chọn loại khách hàng 'Công ty'.");
            SetButtonsInteractable(true);
            return;
        }

        if (_customerLookupStatusText != null) _customerLookupStatusText.text = "Đang kiểm tra thông tin khách hàng...";
        SetButtonsInteractable(false); // Vô hiệu hóa các nút

        // --- 3. LƯU/CẬP NHẬT KHÁCH HÀNG (SỬ DỤNG SALESDATASERVICE) ---
        CustomerData savedCustomerData = null; // Khách hàng sau khi đã lưu/cập nhật
        try
        {
            // Lấy ID khách hàng hiện tại (Local ID hoặc Cloud ID)
            string existingId = _customerManager.GetCurrentCustomerData()?.customerId;

            // Đặt ID Khách hàng cho dữ liệu mới nhất từ UI
            finalCustomerDataFromUI.customerId = existingId;

            // GỌI SALESDATASERVICE ĐỂ LƯU CẢ LÊN CLOUD VÀ XUỐNG LOCAL DB
            string finalCustomerId = await SalesDataService.Instance.SaveCustomerDataAsync(finalCustomerDataFromUI); // Sửa lỗi tiền tố

            // Cập nhật lại đối tượng CustomerData với ID cuối cùng (ID Cloud/Local)
            finalCustomerDataFromUI.customerId = finalCustomerId;
            savedCustomerData = finalCustomerDataFromUI;

            _customerManager.SetCurrentCustomerData(savedCustomerData);
            Debug.Log($"Đã lưu khách hàng thành công với ID: {finalCustomerId}. Nguồn: {(isCloudSyncEnabled ? "Cloud/Local" : "Local Only")}");
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi lưu/cập nhật khách hàng: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi lưu khách hàng. Vui lòng kiểm tra mạng của bạn.";
            }
            _statusPopupManager.ShowPopup(errorMessage);
            Debug.LogError(errorMessage);
            SetButtonsInteractable(true);
            if (_customerLookupStatusText != null) _customerLookupStatusText.text = "Lỗi.";
            return;
        }

        // --- 4. Kiểm tra tồn kho và chuẩn bị SaleItems ---
        List<SaleItem> saleItems = new List<SaleItem>();
        foreach (var productInCart in _cartManager.ProductsInCart.Values)
        {
            if (hasInventoryFeature)
            {
                // Kiểm tra tồn kho chỉ khi có tính năng Inventory
                ProductData actualInventoryProduct = _cartManager.GetProductFromAllUserProducts(productInCart.productId);
                if (actualInventoryProduct == null || productInCart.stock > actualInventoryProduct.stock)
                {
                    _statusPopupManager.ShowPopup($"Không đủ hàng trong kho cho {productInCart.productName}. Tồn kho: {(actualInventoryProduct != null ? actualInventoryProduct.stock : 0)}. Yêu cầu: {productInCart.stock}.");
                    SetButtonsInteractable(true);
                    return;
                }
            }

            saleItems.Add(new SaleItem
            {
                productId = productInCart.productId,
                productName = productInCart.productName ?? "",
                unit = productInCart.unit ?? "",
                quantity = productInCart.stock,
                priceAtSale = productInCart.price
            });
        }

        long finalSubtotal = _cartManager.ProductsInCart.Values.Sum(p => p.price * p.stock);
        long finalTax = (long)(finalSubtotal * TAX_RATE);
        long finalGrandTotal = finalSubtotal + finalTax;

        SaleData newSale = new SaleData
        {
            customerId = savedCustomerData?.customerId ?? "",
            customerName = savedCustomerData?.name ?? "",
            customerPhone = savedCustomerData?.phone ?? "",
            totalAmount = finalGrandTotal,
            taxAmount = finalTax,
            subtotal = finalSubtotal,
            saleDate = Timestamp.FromDateTime(DateTime.UtcNow),
            items = saleItems
        };

        if (_customerLookupStatusText != null) _customerLookupStatusText.text = "Đang hoàn tất đơn hàng...";

        // --- 5. LƯU SALEDATA & CẬP NHẬT TỒN KHO (SỬ DỤNG SALESDATASERVICE) ---
        DocumentReference newSaleDocRef = null;
        try
        {
            // GỌI SALESDATASERVICE ĐỂ LƯU CẢ LÊN CLOUD VÀ XUỐNG LOCAL DB
            string finalSaleId = await SalesDataService.Instance.SaveSaleDataAsync(newSale); // Sửa lỗi tiền tố
            newSale.saleId = finalSaleId;

            // Nếu là Cloud Sync, chúng ta sẽ cần lấy DocumentReference để truyền cho FptInvoiceManager
            if (isCloudSyncEnabled)
            {
                newSaleDocRef = _userSalesCollection.Document(finalSaleId);
            }

            Debug.Log($"Đã lưu đơn hàng thành công với ID: {newSale.saleId}. Nguồn: {(isCloudSyncEnabled ? "Cloud/Local" : "Local Only")}");

            // Logic TRỪ TỒN KHO (Chỉ chạy trên Cloud nếu CloudSync được bật)
            // Logic trừ kho Local sẽ nằm trong SalesDataService nếu cần
            if (hasInventoryFeature && isCloudSyncEnabled)
            {
                WriteBatch batch = _db.StartBatch();
                foreach (var cartProduct in _cartManager.ProductsInCart.Values)
                {
                    DocumentReference productDocRef = _userProductsCollection.Document(cartProduct.productId);
                    batch.Update(productDocRef, "stock", FieldValue.Increment(-cartProduct.stock));
                }
                await batch.CommitAsync();
                Debug.Log("Đã cập nhật tồn kho FIREBASE thành công.");
            }
            else
            {
                Debug.Log("Gói hiện tại không quản lý tồn kho Cloud. Bỏ qua việc trừ tồn kho Firebase.");
            }

        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi khi lưu đơn hàng hoặc cập nhật tồn kho: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi lưu đơn hàng. Vui lòng kiểm tra mạng của bạn.";
            }
            _statusPopupManager.ShowPopup(errorMessage);
            Debug.LogError(errorMessage);
            SetButtonsInteractable(true);
            return;
        }

        // --- 6. Xử lý Hóa đơn điện tử FPT (Chỉ chạy khi có Cloud Sync) ---
        if (_fptInvoiceManager != null && isCloudSyncEnabled)
        {
            var (fptSuccess, fptInvId, fptInvSeq, fptInvSerial, fptLookupLink, fptErrorMsg) =
                await _fptInvoiceManager.ProcessFptInvoiceCreation(savedCustomerData, _cartManager.ProductsInCart, newSale, newSaleDocRef);

            if (fptSuccess)
            {
                // Cập nhật lại SaleData cục bộ với thông tin hóa đơn FPT
                await SalesDataService.Instance.UpdateSaleDataLocallyWithInvoiceInfo(newSale); // Sửa lỗi tiền tố
                Debug.Log("Đơn hàng đã hoàn tất thành công! Hóa đơn điện tử đã được xử lý.");
            }
            else
            {
                // FptInvoiceManager đã hiển thị popup lỗi, chỉ cần log và thông báo chung
                Debug.LogError($"SalesFinalizeTransaction: Lỗi xử lý hóa đơn FPT: {fptErrorMsg}");
                _statusPopupManager.ShowPopup($"Đơn hàng đã hoàn tất, NHƯNG LỖI Hóa đơn điện tử FPT: {fptErrorMsg}");
            }
        }
        else if (isCloudSyncEnabled) // Nếu CloudSync bật nhưng FPT Manager chưa gán
        {
            Debug.LogWarning("SalesFinalizeTransaction: Bỏ qua tạo hóa đơn FPT vì Manager chưa được gán.");
            _statusPopupManager.ShowPopup("Đơn hàng đã hoàn tất thành công!");
        }
        else // Local Only Mode
        {
            Debug.Log("SalesFinalizeTransaction: Hoàn tất giao dịch ở chế độ Local Only.");
            _statusPopupManager.ShowPopup("Đơn hàng đã hoàn tất thành công! (Lưu cục bộ)");
        }


        // --- 7. Hoàn tất giao dịch ---
        Debug.Log("Đơn hàng Bizmate đã hoàn tất quá trình xử lý.");
        OnCancelSaleButtonClicked(); // Reset giỏ hàng và thông tin khách hàng

        SetButtonsInteractable(true);
        if (_customerLookupStatusText != null) _customerLookupStatusText.text = "";
    }


    public void OnCancelSaleButtonClicked()
    {
        _cartManager.ClearCart(); // Xóa giỏ hàng thông qua CartManager
        _customerManager.ClearCustomerInfo(); // Xóa thông tin khách hàng thông qua CustomerManager

        // Reset các trạng thái UI khác nếu cần
        Debug.Log("Đã hủy đơn hàng và reset.");
    }

    private void OnExportInvoiceButtonClicked()
    {
        // Chức năng này được xử lý bởi SalesFptInvoiceManager
        _fptInvoiceManager?.OnExportInvoiceButtonClicked();
    }


    // Hàm tiện ích để điều khiển khả năng tương tác của các nút chính
    private void SetButtonsInteractable(bool interactable)
    {
        if (completeSaleButton != null) completeSaleButton.interactable = interactable;
        if (cancelSaleButton != null) cancelSaleButton.interactable = interactable;
        // exportInvoiceButton được điều khiển bởi SalesFptInvoiceManager hoặc SalesManager chính

    }
}