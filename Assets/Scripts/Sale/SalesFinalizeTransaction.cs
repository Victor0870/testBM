using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ShopSessionData;
using static ShopSettingManager;

public class SalesFinalizeTransaction : MonoBehaviour
{
    [Header("Payment Summary UI")]
    public TMP_Text subtotalText;
    public TMP_Text taxText;
    public TMP_Text grandTotalText;

    // --- MỚI: Ô nhập giảm giá ---
    public TMP_InputField discountInputField;
    // ----------------------------

    public Button completeSaleButton;
    public Button cancelSaleButton;
    public Button exportInvoiceButton;

    private StatusPopupManager _statusPopupManager;
    private FirebaseFirestore _db;
    private FirebaseUser _currentUser;
    private CollectionReference _userSalesCollection;
    private CollectionReference _userProductsCollection;

    private SalesCustomerManager _customerManager;
    private SalesCartManager _cartManager;
    private SalesFptInvoiceManager _fptInvoiceManager;

    private TMP_Text _customerLookupStatusText;
    private bool listenersInitialized = false;

    private const double TAX_RATE = 0.10;
    private long _currentDiscountAmount = 0; // Biến lưu giá trị giảm giá hiện tại

    public void Initialize(FirebaseFirestore dbInstance, FirebaseUser currentUser,
                           CollectionReference userSalesCollection, CollectionReference userProductsCollection,
                           SalesCustomerManager customerManager, SalesCartManager cartManager,
                           SalesFptInvoiceManager fptInvoiceManager, StatusPopupManager statusPopupManager,
                           TMP_Text customerLookupStatusTextRef)
    {
        _db = dbInstance;
        _currentUser = currentUser;
        _userSalesCollection = userSalesCollection;
        _userProductsCollection = userProductsCollection;
        _customerManager = customerManager;
        _cartManager = cartManager;
        _fptInvoiceManager = fptInvoiceManager;
        _statusPopupManager = statusPopupManager;
        _customerLookupStatusText = customerLookupStatusTextRef;

        if (!listenersInitialized)
        {
            if (completeSaleButton != null) completeSaleButton.onClick.AddListener(OnCompleteSaleButtonClicked);
            if (cancelSaleButton != null) cancelSaleButton.onClick.AddListener(OnCancelSaleButtonClicked);
            if (exportInvoiceButton != null) exportInvoiceButton.onClick.AddListener(OnExportInvoiceButtonClicked);

            // --- MỚI: Lắng nghe sự kiện nhập giảm giá ---
            if (discountInputField != null)
            {
                discountInputField.onEndEdit.AddListener(OnDiscountValueChanged);
                discountInputField.text = "0"; // Mặc định là 0
            }
            // --------------------------------------------

            listenersInitialized = true;
        }

        if (_cartManager != null)
        {
            _cartManager.OnCartChanged += UpdateCartSummaryUI;
        }

        UpdateCartSummaryUI();
    }

    // --- MỚI: Xử lý khi nhập giảm giá ---
    private void OnDiscountValueChanged(string value)
    {
        if (long.TryParse(value, out long discount))
        {
            if (discount < 0) discount = 0;
            _currentDiscountAmount = discount;
        }
        else
        {
            _currentDiscountAmount = 0;
            if (discountInputField != null) discountInputField.text = "0";
        }
        UpdateCartSummaryUI(); // Tính lại tổng tiền
    }
    // ------------------------------------

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

        // Logic tính toán: (Tổng hàng - Giảm giá) + Thuế
        // Lưu ý: Thuế thường tính trên (Tổng hàng - Giảm giá) hoặc Tổng hàng tùy chính sách.
        // Ở đây ta tính Thuế trên (Tổng hàng - Giảm giá) để có lợi cho khách.

        long taxableAmount = currentSubtotal - _currentDiscountAmount;
        if (taxableAmount < 0) taxableAmount = 0; // Không để âm

        long currentTax = (long)(taxableAmount * TAX_RATE);
        long currentGrandTotal = taxableAmount + currentTax;

        if (subtotalText != null) subtotalText.text = $"  {currentSubtotal:N0} VNĐ";

        // Hiển thị giảm giá nếu cần (hoặc người dùng nhìn vào InputField)

        if (taxText != null) taxText.text = $" ({TAX_RATE * 100}%): {currentTax:N0} VNĐ";
        if (grandTotalText != null) grandTotalText.text = $"  {currentGrandTotal:N0} VNĐ";

        if (completeSaleButton != null)
        {
            completeSaleButton.interactable = _cartManager != null && _cartManager.ProductsInCart != null && _cartManager.ProductsInCart.Count > 0;
        }
    }

    public async void OnCompleteSaleButtonClicked()
    {
        // ... (Giữ nguyên các kiểm tra ban đầu như code cũ) ...
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        bool hasSalesFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales);
        bool hasInventoryFeature = ShopSessionData.AppPackageConfig != null && ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);
        bool isCloudSyncEnabled = SalesDataService.Instance != null && SalesDataService.Instance.IsCloudSyncEnabled;

        if (_cartManager == null || _cartManager.ProductsInCart == null || _cartManager.ProductsInCart.Count == 0)
        {
            _statusPopupManager.ShowPopup("Giỏ hàng trống.");
            return;
        }
        if (isCloudSyncEnabled && _currentUser == null)
        {
            _statusPopupManager.ShowPopup("Lỗi: Người dùng chưa đăng nhập.");
            return;
        }

        // --- Lấy thông tin khách hàng ---
        CustomerData finalCustomerDataFromUI = _customerManager.GetCustomerDataFromUI();
        if (string.IsNullOrEmpty(finalCustomerDataFromUI.name)) finalCustomerDataFromUI.name = "Khách lẻ";

        if (_customerLookupStatusText != null) _customerLookupStatusText.text = "Đang xử lý...";
        SetButtonsInteractable(false);

        // --- Lưu Khách hàng ---
        CustomerData savedCustomerData = null;
        try
        {
            string existingId = _customerManager.GetCurrentCustomerData()?.customerId;
            finalCustomerDataFromUI.customerId = existingId;
            string finalCustomerId = await SalesDataService.Instance.SaveCustomerDataAsync(finalCustomerDataFromUI);
            finalCustomerDataFromUI.customerId = finalCustomerId;
            savedCustomerData = finalCustomerDataFromUI;
            _customerManager.SetCurrentCustomerData(savedCustomerData);
        }
        catch (Exception e)
        {
            _statusPopupManager.ShowPopup($"Lỗi lưu khách hàng: {e.Message}");
            SetButtonsInteractable(true);
            return;
        }

        // --- Chuẩn bị SaleItems ---
        List<SaleItem> saleItems = new List<SaleItem>();
        foreach (var productInCart in _cartManager.ProductsInCart.Values)
        {
            // Kiểm tra tồn kho (giữ nguyên logic cũ)
            if (hasInventoryFeature)
            {
                ProductData actualInventoryProduct = _cartManager.GetProductFromAllUserProducts(productInCart.productId);
                if (actualInventoryProduct == null || productInCart.stock > actualInventoryProduct.stock)
                {
                    _statusPopupManager.ShowPopup($"Không đủ hàng: {productInCart.productName}.");
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

        // --- Tính toán lại lần cuối ---
        long finalSubtotal = _cartManager.ProductsInCart.Values.Sum(p => p.price * p.stock);
        long taxableAmount = finalSubtotal - _currentDiscountAmount;
        if (taxableAmount < 0) taxableAmount = 0;
        long finalTax = (long)(taxableAmount * TAX_RATE);
        long finalGrandTotal = taxableAmount + finalTax;

        SaleData newSale = new SaleData
        {
            customerId = savedCustomerData?.customerId ?? "",
            customerName = savedCustomerData?.name ?? "",
            customerPhone = savedCustomerData?.phone ?? "",

            // --- MỚI: Lưu các giá trị tính toán ---
            subtotal = finalSubtotal,
            discountAmount = _currentDiscountAmount, // Lưu giảm giá
            taxAmount = finalTax,
            totalAmount = finalGrandTotal,
            // -------------------------------------

            saleDate = Timestamp.FromDateTime(DateTime.UtcNow),
            items = saleItems
        };

        // --- Lưu Đơn hàng & Trừ kho (Giữ nguyên logic cũ) ---
        DocumentReference newSaleDocRef = null;
        try
        {
            string finalSaleId = await SalesDataService.Instance.SaveSaleDataAsync(newSale);
            newSale.saleId = finalSaleId;

            if (isCloudSyncEnabled) newSaleDocRef = _userSalesCollection.Document(finalSaleId);

            if (hasInventoryFeature && isCloudSyncEnabled)
            {
                WriteBatch batch = _db.StartBatch();
                foreach (var cartProduct in _cartManager.ProductsInCart.Values)
                {
                    DocumentReference productDocRef = _userProductsCollection.Document(cartProduct.productId);
                    batch.Update(productDocRef, "stock", FieldValue.Increment(-cartProduct.stock));
                }
                await batch.CommitAsync();
            }
        }
        catch (Exception e)
        {
            _statusPopupManager.ShowPopup($"Lỗi lưu đơn hàng: {e.Message}");
            SetButtonsInteractable(true);
            return;
        }

        // --- Xử lý HĐĐT (Giữ nguyên) ---
        if (_fptInvoiceManager != null && isCloudSyncEnabled)
        {
            var (fptSuccess, fptInvId, fptInvSeq, fptInvSerial, fptLookupLink, fptErrorMsg) =
                await _fptInvoiceManager.ProcessFptInvoiceCreation(savedCustomerData, _cartManager.ProductsInCart, newSale, newSaleDocRef);

            if (fptSuccess) await SalesDataService.Instance.UpdateSaleDataLocallyWithInvoiceInfo(newSale);
            else _statusPopupManager.ShowPopup($"Đơn hàng xong, nhưng lỗi HĐĐT: {fptErrorMsg}");
        }
        else
        {
            _statusPopupManager.ShowPopup("Đơn hàng đã hoàn tất thành công!");
        }

        OnCancelSaleButtonClicked(); // Reset UI
        SetButtonsInteractable(true);
        if (_customerLookupStatusText != null) _customerLookupStatusText.text = "";
    }

    public void OnCancelSaleButtonClicked()
    {
        _cartManager.ClearCart();
        _customerManager.ClearCustomerInfo();

        // --- MỚI: Reset giảm giá ---
        _currentDiscountAmount = 0;
        if (discountInputField != null) discountInputField.text = "0";
        UpdateCartSummaryUI();
        // ---------------------------
    }

    private void OnExportInvoiceButtonClicked()
    {
        _fptInvoiceManager?.OnExportInvoiceButtonClicked();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (completeSaleButton != null) completeSaleButton.interactable = interactable;
        if (cancelSaleButton != null) cancelSaleButton.interactable = interactable;
        if (discountInputField != null) discountInputField.interactable = interactable; // Khóa ô giảm giá khi đang xử lý
    }
}