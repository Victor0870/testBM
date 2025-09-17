using Firebase.Firestore;
using System;

[FirestoreData]
[Serializable]
public class CustomerData
{
    // Trường này dùng để lưu trữ Document ID của khách hàng từ Firestore.
    // KHÔNG gắn [FirestoreProperty] vì đây không phải là một trường dữ liệu trong document Firestore.
    // Document ID được quản lý bởi Firestore và được truy cập thông qua DocumentSnapshot.Id.
    public string customerId { get; set; }

    [FirestoreProperty("phone")]
    public string phone { get; set; }

    [FirestoreProperty("name")] // Tên khách hàng (cá nhân hoặc người liên hệ chính của công ty)
    public string name { get; set; }

    [FirestoreProperty("address")]
    public string address { get; set; }

    // Thêm trường mới cho Mã số thuế
    [FirestoreProperty("taxId")]
    public string taxId { get; set; }

    // THÊM CÁC TRƯỜNG MỚI ĐÃ THẢO LUẬN
    [FirestoreProperty("companyName")] // Tên công ty (nếu là khách hàng công ty)
    public string companyName { get; set; }

    [FirestoreProperty("customerType")] // Loại khách hàng: "Khách lẻ" hoặc "Công ty"
    public string customerType { get; set; }

    [FirestoreProperty("idNumber")] // CCCD/Số định danh cá nhân (nếu là khách lẻ)
    public string idNumber { get; set; }

    // Constructor mặc định cần thiết cho Firestore để deserialize dữ liệu
    public CustomerData() { }
}