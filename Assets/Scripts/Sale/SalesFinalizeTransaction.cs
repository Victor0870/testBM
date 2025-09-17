// File: SalesFinalizeTransaction.cs
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
// Không cần using SimpleJSON ở đây vì SalesFptInvoiceManager đã xử lý nó

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
        if (!listenersInitialized) // <-- THÊM ĐIỀU KIỆN NÀY
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
        // --- 1. Kiểm tra điều kiện ban đầu (đã di chuyển từ SalesManager) ---

        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasSalesFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales);
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);
        bool hasEInvoiceFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.EInvoice);

        if (_cartManager == null || _cartManager.ProductsInCart == null || _cartManager.ProductsInCart.Count == 0)
        {
            _statusPopupManager.ShowPopup("Giỏ hàng trống. Không thể hoàn tất đơn hàng.");
            return;
        }
        if (_currentUser == null)
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

        // --- 3. Lưu/Cập nhật khách hàng vào Firestore ---
        CustomerData savedCustomerData = null; // Khách hàng sau khi đã lưu/cập nhật vào Firestore
        try
        {
            if (_customerManager.GetCurrentCustomerData() == null || string.IsNullOrEmpty(_customerManager.GetCurrentCustomerData().customerId))
            {
                // Khách hàng mới: Add vào Firestore
                DocumentReference newCustomerDocRef = await SalesManager.Instance.db
                    .Collection("shops")
                    .Document(SalesManager.Instance.currentUser.UserId)
                    .Collection("customers")
                    .AddAsync(finalCustomerDataFromUI);
                savedCustomerData = finalCustomerDataFromUI;
                savedCustomerData.customerId = newCustomerDocRef.Id;
                Debug.Log($"Đã lưu khách hàng mới: {savedCustomerData.name} (ID: {savedCustomerData.customerId})");
            }
            else
            {
                // Khách hàng đã tồn tại: Cập nhật nếu có thay đổi và đang ở chế độ chỉnh sửa
                // Sử dụng lại logic kiểm tra thay đổi từ SalesManager gốc để tránh update không cần thiết
                CustomerData existingCustomer = _customerManager.GetCurrentCustomerData();
                if (existingCustomer.name != finalCustomerDataFromUI.name ||
                    existingCustomer.address != finalCustomerDataFromUI.address ||
                    existingCustomer.taxId != finalCustomerDataFromUI.taxId ||
                    existingCustomer.companyName != finalCustomerDataFromUI.companyName ||
                    existingCustomer.customerType != finalCustomerDataFromUI.customerType ||
                    existingCustomer.idNumber != finalCustomerDataFromUI.idNumber)
                {
                    DocumentReference customerDocRef = SalesManager.Instance.db
                        .Collection("shops")
                        .Document(SalesManager.Instance.currentUser.UserId)
                        .Collection("customers")
                        .Document(existingCustomer.customerId);
                    await customerDocRef.SetAsync(finalCustomerDataFromUI); // SetAsync sẽ ghi đè
                    savedCustomerData = finalCustomerDataFromUI;
                    savedCustomerData.customerId = existingCustomer.customerId; // Giữ lại ID cũ
                    Debug.Log($"Đã cập nhật thông tin khách hàng: {savedCustomerData.name} (ID: {savedCustomerData.customerId})");
                }
                else
                {
                    savedCustomerData = existingCustomer; // Không có thay đổi, dùng dữ liệu cũ
                }
            }
            _customerManager.SetCurrentCustomerData(savedCustomerData); // Cập nhật lại trong CustomerManager
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
                ProductData actualInventoryProduct = _cartManager.GetProductFromAllUserProducts(productInCart.productId); // Lấy sản phẩm từ danh sách tổng
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

        // --- 5. Lưu SaleData và Cập nhật tồn kho ---
        DocumentReference newSaleDocRef = null;
        try
        {
            newSaleDocRef = await _userSalesCollection.AddAsync(newSale);
            newSale.saleId = newSaleDocRef.Id;
            Debug.Log($"Đã lưu đơn hàng thành công vào Firestore với ID: {newSale.saleId}");

            if (hasInventoryFeature)
            {
                WriteBatch batch = _db.StartBatch();
                foreach (var cartProduct in _cartManager.ProductsInCart.Values)
                {
                    DocumentReference productDocRef = _userProductsCollection.Document(cartProduct.productId);
                    batch.Update(productDocRef, "stock", FieldValue.Increment(-cartProduct.stock));
                }
                await batch.CommitAsync();
                Debug.Log("Đã cập nhật tồn kho thành công.");
            }
            else
            {
                Debug.Log("Gói hiện tại không quản lý tồn kho. Bỏ qua việc trừ tồn kho.");
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

        // --- 6. Xử lý Hóa đơn điện tử FPT (nếu có quyền) ---
        if (_fptInvoiceManager != null)
        {
            var (fptSuccess, fptInvId, fptInvSeq, fptInvSerial, fptLookupLink, fptErrorMsg) = 
                await _fptInvoiceManager.ProcessFptInvoiceCreation(savedCustomerData, _cartManager.ProductsInCart, newSale, newSaleDocRef);

            if (fptSuccess)
            {
                Debug.Log("Đơn hàng đã hoàn tất thành công! Hóa đơn điện tử đã được xử lý.");
            }
            else
            {
                // FptInvoiceManager đã hiển thị popup lỗi, chỉ cần log và thông báo chung
                Debug.LogError($"SalesFinalizeTransaction: Lỗi xử lý hóa đơn FPT: {fptErrorMsg}");
                _statusPopupManager.ShowPopup($"Đơn hàng đã hoàn tất, NHƯNG LỖI Hóa đơn điện tử FPT: {fptErrorMsg}");
            }
        }
        else
        {
            Debug.LogWarning("SalesFinalizeTransaction: FPT eInvoice Manager chưa được gán. Bỏ qua việc tạo hóa đơn điện tử FPT.");
            _statusPopupManager.ShowPopup("Đơn hàng đã hoàn tất thành công! (Không tạo hóa đơn FPT)");
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
        // _customerLookupStatusText.text = ""; // Already handled in OnCompleteSaleButtonClicked.finally
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