using Firebase.Firestore;
using System;

[FirestoreData]
[Serializable]
public class SlipItemData
{
    // KHÔNG cần productId/documentId riêng vì nó là một phần của danh sách lồng nhau.

    [FirestoreProperty("productId")]
    public string productId { get; set; }

    [FirestoreProperty("productName")] // Thêm tên sản phẩm để dễ dàng hiển thị trong lịch sử
    public string productName { get; set; }

    [FirestoreProperty("quantity")]
    public long quantity { get; set; }

    [FirestoreProperty("importPrice")]
    public long importPrice { get; set; } // Giá nhập của riêng lô hàng này

    // Constructor mặc định cần thiết cho Firestore
    public SlipItemData() { }
}