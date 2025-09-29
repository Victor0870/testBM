// File: Scripts/SalesInvoice/SaleOrderDetailPanel.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
// ĐÃ XÓA: using SalesDataService; // CS0138
using static ShopSessionData;
using static ShopSettingManager;
using System.Threading.Tasks;

public class SaleOrderDetailPanel : MonoBehaviour
{
    // --- UI GỐC VÀ ĐÓNG ---
    [Header("Panel Root & Navigation")]
    public GameObject panelRoot;
    public Button closeButton;
    public StatusPopupManager statusPopupManager; // Cần gán từ Inspector

    // --- THÔNG TIN TÓM TẮT ---
    [Header("Sale Summary")]
    public TMP_Text saleIdText;
    public TMP_Text dateText;
    public TMP_Text totalText;

    // --- THÔNG TIN KHÁCH HÀNG ---
    [Header("Customer Info")]
    public TMP_Text customerNameText;
    public TMP_Text customerPhoneText;
    public TMP_Text customerTypeTaxText; // Hiển thị Loại KH và MST/CCCD
    public TMP_Text customerAddressText;

    // --- DANH SÁCH SẢN PHẨM ---
    [Header("Item List")]
    public Transform itemListContentParent; // Container cho danh sách SaleItem
    public GameObject saleItemDetailPrefab; // Prefab cho từng dòng SaleItem
    public TMP_Text saleItemsListText; // Dùng tạm 1 Text nếu không có Prefab chi tiết

    // --- THÔNG TIN HÓA ĐƠN ---
    [Header("Invoice Info")]
    public TMP_Text invoiceStatusText;
    public TMP_Text invoiceSeqSerialText;
    public TMP_Text invoiceLookupLinkText;

    // --- HÀNH ĐỘNG ---
    [Header("Actions")]
    public Button exportInvoiceButton; // Xuất HĐĐT (Chỉ khi chưa xuất)
    public Button lookupButton;        // Tra cứu HĐĐT (Chỉ khi đã xuất)

    private SaleData currentSaleData;
    private CustomerData currentCustomerSnapshot;

    // Tham chiếu đến SalesFptInvoiceManager và SalesInvoiceManager
    private SalesFptInvoiceManager fptInvoiceManager;
    private SalesInvoiceManager salesInvoiceManager;

    void Awake()
    {
        panelRoot?.SetActive(false);
        closeButton?.onClick.AddListener(HidePanel);
        exportInvoiceButton?.onClick.AddListener(OnExportInvoiceButtonClicked);
        lookupButton?.onClick.AddListener(OnLookupButtonClicked);

        // Cố gắng tìm các Manager từ Scene
        fptInvoiceManager = FindObjectOfType<SalesFptInvoiceManager>();
        salesInvoiceManager = FindObjectOfType<SalesInvoiceManager>();
    }

    public async void ShowPanel(SaleData sale)
    {
        if (sale == null) return;

        SetInteractable(false);
        currentSaleData = sale;

        saleIdText.text = $"ID Đơn hàng: {sale.saleId ?? "N/A"}";
        dateText.text = $"Ngày: {sale.saleDate.ToDateTime().ToLocalTime():dd/MM/yyyy HH:mm}";
        totalText.text = $"Tổng tiền: {sale.totalAmount:N0} VNĐ";

        // 1. Tải thông tin Khách hàng chi tiết (nếu có Cloud Sync)
        if (SalesDataService.Instance != null)
        {
            currentCustomerSnapshot = await SalesDataService.Instance.GetCustomerDataBySaleIdAsync(sale.customerId);
        }

        // 2. Cập nhật UI
        UpdateCustomerUI(sale);
        UpdateSaleItemsUI(sale.items);
        UpdateInvoiceUI(sale);

        panelRoot?.SetActive(true);
        SetInteractable(true);
    }

    private void HidePanel()
    {
        panelRoot?.SetActive(false);
    }

    private void UpdateCustomerUI(SaleData sale)
    {
        customerNameText.text = $"Khách hàng: {sale.customerName ?? "Khách lẻ"}";
        customerPhoneText.text = $"SĐT: {sale.customerPhone ?? "N/A"}";

        // Sử dụng snapshot (currentCustomerSnapshot) nếu tìm thấy để hiển thị chi tiết hơn
        if (currentCustomerSnapshot != null)
        {
            customerTypeTaxText.text = $"{currentCustomerSnapshot.customerType ?? "N/A"} - MST: {currentCustomerSnapshot.taxId ?? "N/A"}";
            customerAddressText.text = $"Địa chỉ: {currentCustomerSnapshot.address ?? "N/A"}";
        }
        else
        {
            // Hiển thị thông tin tối thiểu từ SaleData
            customerTypeTaxText.text = "Không tìm thấy chi tiết khách hàng.";
            customerAddressText.text = "";
        }
    }

    private void UpdateSaleItemsUI(List<SaleItem> items)
    {
        // Xóa nội dung cũ (nếu dùng Prefab)
        if (itemListContentParent != null)
        {
            foreach (Transform child in itemListContentParent)
            {
                Destroy(child.gameObject);
            }
        }

        if (items == null || items.Count == 0) return;

        // Dùng tạm Text để hiển thị danh sách sản phẩm
        string itemsList = "Chi tiết đơn hàng:\n";
        foreach (var item in items)
        {
            itemsList += $"- {item.productName}: {item.quantity} x {item.priceAtSale:N0} = {item.quantity * item.priceAtSale:N0} VNĐ\n";
        }
        if(saleItemsListText != null) saleItemsListText.text = itemsList;
    }

    private void UpdateInvoiceUI(SaleData sale)
    {
        bool hasInvoice = !string.IsNullOrEmpty(sale.fptInvoiceId);
        bool hasEInvoiceFeature = AppPackageConfig != null && AppPackageConfig.HasFeature(CachedShopSettings?.packageType, AppFeature.EInvoice);
        bool isCloudSync = SalesDataService.Instance != null && SalesDataService.Instance.IsCloudSyncEnabled;

        // Trạng thái HĐĐT
        if (hasInvoice)
        {
            invoiceStatusText.text = "Đã phát hành HĐĐT thành công";
            invoiceStatusText.color = Color.green;
            invoiceSeqSerialText.text = $"Số: {sale.fptInvoiceSeq ?? "N/A"} | Ký hiệu: {sale.fptInvoiceSerial ?? "N/A"}";
            invoiceLookupLinkText.text = "Link tra cứu: Đã có";
            exportInvoiceButton.gameObject.SetActive(false);
            lookupButton.interactable = !string.IsNullOrEmpty(sale.fptLookupLink);
        }
        else
        {
            invoiceStatusText.text = "Chưa phát hành HĐĐT";
            invoiceStatusText.color = Color.red;
            invoiceSeqSerialText.text = "";
            invoiceLookupLinkText.text = "";

            // Kích hoạt nút Xuất HĐĐT nếu có quyền Cloud Sync VÀ EInvoice
            exportInvoiceButton.gameObject.SetActive(true);
            exportInvoiceButton.interactable = hasEInvoiceFeature && isCloudSync;
            if (!exportInvoiceButton.interactable)
            {
                 StatusPopupManager.Instance.ShowPopup("Để xuất hóa đơn điện tử, bạn cần gói Pro.");
            }

            lookupButton.interactable = false;
        }
    }

    private async void OnExportInvoiceButtonClicked()
    {
        if (currentSaleData == null || fptInvoiceManager == null) return;

        // Kiểm tra lại quyền EInvoice và CloudSync
        bool hasEInvoiceFeature = AppPackageConfig != null && AppPackageConfig.HasFeature(CachedShopSettings?.packageType, AppFeature.EInvoice);
        if (!hasEInvoiceFeature || !SalesDataService.Instance.IsCloudSyncEnabled)
        {
            statusPopupManager.ShowPopup("Tính năng Xuất HĐĐT chỉ dành cho gói Pro (Cloud Sync).");
            return;
        }

        SetInteractable(false);

        try
        {
            // B1: Lấy lại ProductData từ SaleItem (Chỉ lấy Name, Quantity, PriceAtSale, Unit)
            Dictionary<string, ProductData> productsInCartSnapshot = SalesDataService.Instance.ConvertSaleItemsToProductSnapshot(currentSaleData.items);

            // B2: Thực hiện xuất hóa đơn
            // newSaleDocRef = null vì SaleData đã được lưu, FPT Manager sẽ tự tìm lại DocumentReference
            var (fptSuccess, fptInvId, fptInvSeq, fptInvSerial, fptLookupLink, fptErrorMsg) =
                 await fptInvoiceManager.ProcessFptInvoiceCreation(
                    currentCustomerSnapshot, // CustomerData (snapshot)
                    productsInCartSnapshot,
                    currentSaleData,
                    null
            );

            if (fptSuccess)
            {
                // Cập nhật SaleData cục bộ
                currentSaleData.fptInvoiceId = fptInvId;
                currentSaleData.fptInvoiceSeq = fptInvSeq;
                currentSaleData.fptInvoiceSerial = fptInvSerial;
                currentSaleData.fptLookupLink = fptLookupLink;

                // Cần gọi SalesDataService để cập nhật lại Local DB và kích hoạt Listener trên Cloud
                await SalesDataService.Instance.UpdateSaleDataLocallyWithInvoiceInfo(currentSaleData);

                // Cập nhật lại Panel chi tiết
                ShowPanel(currentSaleData);
                statusPopupManager.ShowPopup("Đã xuất Hóa đơn điện tử thành công!");
            }
            else
            {
                 statusPopupManager.ShowPopup($"Lỗi khi xuất HĐĐT: {fptErrorMsg}");
            }
        }
        catch (Exception e)
        {
            statusPopupManager.ShowPopup($"Lỗi nghiêm trọng khi xuất HĐĐT: {e.GetBaseException().Message}");
            Debug.LogError($"Lỗi khi xuất HĐĐT: {e.Message}");
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private void OnLookupButtonClicked()
    {
        if (currentSaleData != null && !string.IsNullOrEmpty(currentSaleData.fptLookupLink))
        {
             Application.OpenURL(currentSaleData.fptLookupLink);
        }
        else
        {
             statusPopupManager.ShowPopup("Không tìm thấy liên kết tra cứu hóa đơn.");
        }
    }

    private void SetInteractable(bool interactable)
    {
        if (panelRoot != null)
        {
            CanvasGroup canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.7f;
        }
    }
}