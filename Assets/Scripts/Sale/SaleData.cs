// File: SaleData.cs
using Firebase.Firestore;
using System;
using System.Collections.Generic;

[FirestoreData]
[Serializable]
public class SaleData
{
    // Trường này KHÔNG gắn [FirestoreProperty] vì đây là Document ID từ Firestore.
    // Nó sẽ được gán sau khi Document được thêm vào Firestore.
    public string saleId { get; set; } // <-- THÊM: ID của đơn hàng trong Firestore

    // ID của khách hàng (nếu có, từ collection customers)
    [FirestoreProperty("customerId")]
    public string customerId { get; set; }

    // Tên khách hàng tại thời điểm bán (để không bị ảnh hưởng nếu tên khách hàng thay đổi sau này)
    [FirestoreProperty("customerName")]
    public string customerName { get; set; }

    [FirestoreProperty("customerPhone")]
    public string customerPhone { get; set; }

    [FirestoreProperty("totalAmount")]
    public long totalAmount { get; set; } // Tổng cộng sau thuế

    [FirestoreProperty("taxAmount")]
    public long taxAmount { get; set; } // Tiền thuế

    [FirestoreProperty("subtotal")]
    public long subtotal { get; set; } // Tổng tiền hàng trước thuế

    [FirestoreProperty("saleDate")]
    public Timestamp saleDate { get; set; } // Thời gian tạo đơn hàng

    [FirestoreProperty("items")]
    public List<SaleItem> items { get; set; } // Danh sách các sản phẩm trong đơn hàng

    // <-- THÊM CÁC TRƯỜNG MỚI ĐỂ LƯU THÔNG TIN HÓA ĐƠN FPT E-INVOICE -->
    [FirestoreProperty("fptInvoiceId")]
    public string fptInvoiceId { get; set; } // ID giao dịch (sid) của hóa đơn FPT

    [FirestoreProperty("fptInvoiceSeq")]
    public string fptInvoiceSeq { get; set; } // Số hóa đơn được FPT cấp

    [FirestoreProperty("fptInvoiceSerial")]
    public string fptInvoiceSerial { get; set; } // Ký hiệu hóa đơn được FPT cấp

    [FirestoreProperty("fptLookupLink")]
    public string fptLookupLink { get; set; } // Link tra cứu hóa đơn từ FPT
    // <-- KẾT THÚC CÁC TRƯỜNG MỚI -->

    public SaleData() { }
}