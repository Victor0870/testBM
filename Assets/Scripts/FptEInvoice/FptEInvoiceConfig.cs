// File: FptEInvoiceConfig.cs
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "FptEInvoiceConfig", menuName = "Bizmate/FPT eInvoice Config", order = 1)]
public class FptEInvoiceConfig : ScriptableObject
{
    [Header("Default API Credentials (Fallback/Reference Only)")]
    // Các trường này chỉ để tham khảo hoặc dùng làm giá trị mặc định ban đầu khi tạo shop mới.
    // Dữ liệu thực tế cho từng user sẽ được lưu trong Firestore (ShopData) và ShopSessionData.
   // public string apiUsername;
   // public string apiPassword;
   // public string sellerTaxId; // Mã số thuế người bán mặc định (nếu không có trong shop settings)

    [Header("API Endpoints")]
    // Các URL này vẫn là cố định cho môi trường UAT/Production
    public string signInUrl = "https://api-uat.einvoice.fpt.com.vn/c_signin"; // URL đăng nhập để lấy token
    public string createInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/create-icr"; // URL tạo/phát hành hóa đơn mới
    public string updateInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/update-icr"; // URL cập nhật hóa đơn
    public string deleteInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/delete-icr"; // URL xóa hóa đơn
    public string adjustInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/adjust-icr"; // URL điều chỉnh hóa đơn
    public string replaceInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/replace-icr"; // URL thay thế hóa đơn
    public string searchInvoiceUrl = "https://api-uat.einvoice.fpt.com.vn/search-icr"; // URL tra cứu hóa đơn

    // --- CÁC TRƯỜNG NÀY KHÔNG CÒN ĐƯỢC DÙNG TRỰC TIẾP ĐỂ QUẢN LÝ TOKEN RUNTIME NỮA ---
    // Token runtime sẽ được lưu trong ShopData của người dùng và ShopSessionData
    // [Header("Runtime Token Management (Do not edit in Inspector)")]
    // [SerializeField] private string _accessToken;
    // [SerializeField] private long _tokenExpiryTime; // Unix timestamp in seconds

    // public string AccessToken
    // {
    //     get { return _accessToken; }
    //     set { _accessToken = value; }
    // }

    // public long TokenExpiryTime
    // {
    //     get { return _tokenExpiryTime; }
    //     set { _tokenExpiryTime = value; }
    // }

    // Logic kiểm tra token cũng sẽ nằm trong FptEInvoiceApiClient và dựa vào ShopSessionData
    // public bool IsTokenValid()
    // {
    //     if (string.IsNullOrEmpty(_accessToken))
    //     {
    //         return false;
    //     }
    //     long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    //     return _tokenExpiryTime > (currentTime + 3600);
    // }
}