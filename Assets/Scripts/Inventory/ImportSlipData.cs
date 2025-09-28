using Firebase.Firestore;
using System;
using System.Collections.Generic;

[FirestoreData]
[Serializable]
public class ImportSlipData
{
    // Trường này dùng để lưu trữ Document ID từ Firestore.
    // Tương đương với f_name trong BGDatabase.
    public string documentId { get; set; }

    [FirestoreProperty("slipId")]
    public string slipId { get; set; } // Số phiếu nội bộ (Internal Key)

    [FirestoreProperty("importDate")]
    public Timestamp importDate { get; set; } // Sử dụng Timestamp cho ngày giờ

    [FirestoreProperty("supplierId")]
    public string supplierId { get; set; }

    [FirestoreProperty("supplierName")]
    public string supplierName { get; set; } // Thêm tên nhà cung cấp

    [FirestoreProperty("totalValue")]
    public long totalValue { get; set; } // Tổng giá trị phiếu

    // Danh sách các mặt hàng đã nhập (sẽ được lưu trữ lồng nhau trong Firestore)
    [FirestoreProperty("items")]
    public List<SlipItemData> items { get; set; }

    // Constructor mặc định cần thiết cho Firestore
    public ImportSlipData()
    {
        items = new List<SlipItemData>();
        // Thiết lập ngày giờ mặc định khi tạo mới
        importDate = Timestamp.FromDateTime(DateTime.UtcNow);
    }
}