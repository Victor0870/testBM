// File: SaleData.cs
using Firebase.Firestore;
using System;
using System.Collections.Generic;

[FirestoreData]
[Serializable]
public class SaleData
{
    public string saleId { get; set; }

    [FirestoreProperty("customerId")]
    public string customerId { get; set; }

    [FirestoreProperty("customerName")]
    public string customerName { get; set; }

    [FirestoreProperty("customerPhone")]
    public string customerPhone { get; set; }

    [FirestoreProperty("totalAmount")]
    public long totalAmount { get; set; } // Tổng cộng sau thuế và sau giảm giá

    [FirestoreProperty("taxAmount")]
    public long taxAmount { get; set; }

    [FirestoreProperty("subtotal")]
    public long subtotal { get; set; } // Tổng tiền hàng

    // --- MỚI: Thêm trường giảm giá ---
    [FirestoreProperty("discountAmount")]
    public long discountAmount { get; set; } // Số tiền giảm giá trực tiếp
    // --------------------------------

    [FirestoreProperty("saleDate")]
    public Timestamp saleDate { get; set; }

    [FirestoreProperty("items")]
    public List<SaleItem> items { get; set; }

    [FirestoreProperty("fptInvoiceId")]
    public string fptInvoiceId { get; set; }

    [FirestoreProperty("fptInvoiceSeq")]
    public string fptInvoiceSeq { get; set; }

    [FirestoreProperty("fptInvoiceSerial")]
    public string fptInvoiceSerial { get; set; }

    [FirestoreProperty("fptLookupLink")]
    public string fptLookupLink { get; set; }

    public SaleData() { }
}