// File: Scripts/SalesInvoice/SaleOrderUIItem.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Firebase.Firestore;
using static ShopSessionData;

public class SaleOrderUIItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text saleIdText;
    public TMP_Text customerNameText;
    public TMP_Text totalAmountText;
    public TMP_Text dateText;
    public TMP_Text invoiceStatusText; // Hiển thị trạng thái HĐĐT
    public Button viewDetailsButton; // Hoặc Button/Toggle của toàn bộ Item

    private SaleData currentSaleData;
    private Action<SaleData> onViewDetailsRequested;

    void Awake()
    {
        // Gán sự kiện cho nút/item để xem chi tiết
        viewDetailsButton?.onClick.AddListener(OnViewDetailsButtonClicked);
    }

    public void SetSaleData(SaleData data, Action<SaleData> viewDetailsCallback)
    {
        currentSaleData = data;
        onViewDetailsRequested = viewDetailsCallback;

        if (saleIdText != null) saleIdText.text = $"ID: {data.saleId ?? "N/A"}";
        if (customerNameText != null) customerNameText.text = data.customerName ?? "Khách lẻ";

        if (totalAmountText != null)
        {
            totalAmountText.text = $"{data.totalAmount:N0} VNĐ";
        }

        if (dateText != null && data.saleDate != null)
        {
            // Chuyển Timestamp sang giờ địa phương để hiển thị
            DateTime localDate = data.saleDate.ToDateTime().ToLocalTime();
            dateText.text = localDate.ToString("dd/MM/yyyy HH:mm");
        }

        UpdateInvoiceStatus(data);
    }

    private void UpdateInvoiceStatus(SaleData data)
    {
        if (invoiceStatusText != null)
        {
            // Kiểm tra trạng thái Hóa đơn điện tử dựa trên fptInvoiceId
            bool hasInvoice = !string.IsNullOrEmpty(data.fptInvoiceId) || !string.IsNullOrEmpty(data.fptInvoiceSeq);

            if (hasInvoice)
            {
                invoiceStatusText.text = $"Đã xuất HĐĐT (Số: {data.fptInvoiceSeq ?? "N/A"})";
                invoiceStatusText.color = Color.green;
            }
            else
            {
                // Giả định gói Basic không có quyền xuất HĐĐT hoặc chưa xuất
                invoiceStatusText.text = "Chưa xuất HĐĐT";
                invoiceStatusText.color = Color.red;
            }
        }
    }

    private void OnViewDetailsButtonClicked()
    {
        // Kích hoạt callback để SalesInvoiceManager mở Panel Chi tiết
        onViewDetailsRequested?.Invoke(currentSaleData);
    }
}