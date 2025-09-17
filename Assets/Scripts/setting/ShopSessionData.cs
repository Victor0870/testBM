// File: ShopSessionData.cs
using UnityEngine; // Cần cho PlayerPrefs
using System; // Cần cho DateTime
using Firebase.Firestore; // Cần cho Timestamp
using static ShopSettingManager; // Cần để sử dụng ShopData class

// Đảm bảo bạn có đủ các using cho các class mới
// Nếu PackageConfigData.cs và GlobalAppConfigData nằm trong một namespace khác,
// bạn cần thêm using đó vào đây. Ví dụ: using YourProjectNamespace.Config;
// Tuy nhiên, nếu chúng không có namespace nào, thì không cần thêm.
// Giả định rằng GlobalAppConfigData và PackageConfig không có namespace riêng hoặc nằm trong Global namespace.


public static class ShopSessionData
{
    // CachedUserId dùng để kiểm tra xem dữ liệu cache có thuộc về người dùng hiện tại không
    public static string CachedUserId { get; private set; }

    // CachedShopSettings sẽ giữ toàn bộ dữ liệu shop đã tải về/lưu
    public static ShopData CachedShopSettings { get; private set; }

    // Biến tĩnh để giữ tham chiếu đến PackageConfig (ScriptableObject)
    public static PackageConfig AppPackageConfig { get; private set; }

    // Biến tĩnh để giữ cấu hình gói toàn cầu tải từ Firestore
    public static GlobalAppConfigData GlobalAppConfig { get; private set; }


    /// <summary>
    /// Phương thức để khởi tạo AppPackageConfig từ một Asset được kéo vào Inspector.
    /// Nên gọi một lần khi ứng dụng khởi động (ví dụ: từ AuthManager).
    /// </summary>
    /// <param name="config">Asset PackageConfig đã tạo.</param>
    public static void InitializePackageConfig(PackageConfig config)
    {
        if (AppPackageConfig == null) // Chỉ khởi tạo một lần
        {
            AppPackageConfig = config;
            Debug.Log("ShopSessionData: PackageConfig Asset đã được khởi tạo.");
        }
    }

    /// <summary>
    /// Thiết lập GlobalAppConfigData đã tải từ Firestore.
    /// </summary>
    /// <param name="globalConfig">Đối tượng GlobalAppConfigData tải từ Firestore.</param>
    public static void SetGlobalAppConfig(GlobalAppConfigData globalConfig)
    {
        GlobalAppConfig = globalConfig;
        Debug.Log("ShopSessionData: Global App Config đã được thiết lập.");
    }


    /// <summary>
    /// Cập nhật dữ liệu shop vào bộ nhớ cache và lưu vào PlayerPrefs.
    /// </summary>
    /// <param name="userId">ID của người dùng hiện tại.</param>
    /// <param name="shopData">Dữ liệu shop muốn cache.</param>
    public static void SetCachedShopSettings(string userId, ShopData shopData)
    {
        CachedUserId = userId;
        CachedShopSettings = shopData;
        SaveShopDataToPlayerPrefs(userId, shopData); // Lưu vào PlayerPrefs ngay khi cache được cập nhật
    }

    /// <summary>
    /// Tải dữ liệu shop từ PlayerPrefs vào cache khi app khởi động.
    /// </summary>
    /// <returns>True nếu có dữ liệu được tải thành công và hợp lệ, False nếu không có hoặc không hợp lệ.</returns>
    public static bool LoadFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey("CachedUserId"))
        {
            CachedUserId = PlayerPrefs.GetString("CachedUserId");

            CachedShopSettings = new ShopData();
            CachedShopSettings.shopName = PlayerPrefs.GetString("ShopName", "");
            CachedShopSettings.phoneNumber = PlayerPrefs.GetString("PhoneNumber", "");
            CachedShopSettings.taxId = PlayerPrefs.GetString("TaxId", "");
            CachedShopSettings.industry = PlayerPrefs.GetString("Industry", "");

            CachedShopSettings.eInvoiceProvider = PlayerPrefs.GetString("EInvoiceProvider", "");
            CachedShopSettings.eInvoiceUser = PlayerPrefs.GetString("EInvoiceUser", "");
            CachedShopSettings.eInvoicePass = PlayerPrefs.GetString("EInvoicePass", "");
            CachedShopSettings.invoiceSerial = PlayerPrefs.GetString("InvoiceSerial", "");
            CachedShopSettings.invoiceForm = PlayerPrefs.GetString("InvoiceForm", "");
            CachedShopSettings.invoiceType = PlayerPrefs.GetString("InvoiceType", "");

            CachedShopSettings.fptAccessToken = PlayerPrefs.GetString("FptAccessToken", "");
            if (long.TryParse(PlayerPrefs.GetString("FptTokenExpiryTime", "0"), out long expiryTime))
            {
                CachedShopSettings.fptTokenExpiryTime = expiryTime;
            }
            else
            {
                CachedShopSettings.fptTokenExpiryTime = 0;
            }

            // Thêm tải packageType từ PlayerPrefs
            CachedShopSettings.packageType = PlayerPrefs.GetString("PackageType", "");

            long licenseEndUnix = 0;
            if (long.TryParse(PlayerPrefs.GetString("LicenseEndDate", "0"), out licenseEndUnix))
            {
                if (licenseEndUnix > 0 && DateTimeOffset.FromUnixTimeSeconds(licenseEndUnix).UtcDateTime > DateTime.MinValue)
                {
                    CachedShopSettings.licenseEndDate = Timestamp.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(licenseEndUnix).UtcDateTime);
                    Debug.Log($"ShopSessionData: Đã tải LicenseEndDate từ PlayerPrefs: {CachedShopSettings.licenseEndDate.ToDateTime().ToLocalTime():dd/MM/yyyy HH:mm}");
                }
                else
                {
                    Debug.LogWarning("ShopSessionData: LicenseEndDate trong PlayerPrefs không hợp lệ (0 hoặc quá cũ). Bỏ qua giá trị này.");
                    CachedShopSettings.licenseEndDate = Timestamp.FromDateTime(DateTime.MinValue.ToUniversalTime());
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("ShopSessionData: Không thể parse LicenseEndDate từ PlayerPrefs. Bỏ qua giá trị này.");
                CachedShopSettings.licenseEndDate = Timestamp.FromDateTime(DateTime.MinValue.ToUniversalTime());
                return false;
            }

            Debug.Log("ShopSessionData: Đã tải shop settings từ PlayerPrefs.");
            return true;
        }
        Debug.Log("ShopSessionData: No cached user data found in PlayerPrefs.");
        return false;
    }

    /// <summary>
    /// Lưu dữ liệu shop từ cache vào PlayerPrefs.
    /// </summary>
    private static void SaveShopDataToPlayerPrefs(string userId, ShopData shopData)
    {
        PlayerPrefs.SetString("CachedUserId", userId);
        PlayerPrefs.SetString("ShopName", shopData.shopName);
        PlayerPrefs.SetString("PhoneNumber", shopData.phoneNumber);
        PlayerPrefs.SetString("TaxId", shopData.taxId);
        PlayerPrefs.SetString("Industry", shopData.industry);

        PlayerPrefs.SetString("EInvoiceProvider", shopData.eInvoiceProvider);
        PlayerPrefs.SetString("EInvoiceUser", shopData.eInvoiceUser);
        PlayerPrefs.SetString("EInvoicePass", shopData.eInvoicePass);
        PlayerPrefs.SetString("InvoiceSerial", shopData.invoiceSerial);
        PlayerPrefs.SetString("InvoiceForm", shopData.invoiceForm);
        PlayerPrefs.SetString("InvoiceType", shopData.invoiceType);

        PlayerPrefs.SetString("FptAccessToken", shopData.fptAccessToken);
        PlayerPrefs.SetString("FptTokenExpiryTime", shopData.fptTokenExpiryTime.ToString());

        // Thêm lưu packageType vào PlayerPrefs
        PlayerPrefs.SetString("PackageType", shopData.packageType);

        if (shopData.licenseEndDate != null)
        {
            PlayerPrefs.SetString("LicenseEndDate", shopData.licenseEndDate.ToDateTimeOffset().ToUnixTimeSeconds().ToString());
            Debug.Log($"ShopSessionData: Đã lưu LicenseEndDate vào PlayerPrefs: {shopData.licenseEndDate.ToDateTime().ToLocalTime():dd/MM/yyyy HH:mm}");
        }
        else
        {
            PlayerPrefs.DeleteKey("LicenseEndDate");
            Debug.Log("ShopSessionData: LicenseEndDate là null, đã xóa khỏi PlayerPrefs.");
        }

        PlayerPrefs.Save();
        Debug.Log("ShopSessionData: Saved shop settings to PlayerPrefs.");
    }

    /// <summary>
    /// Xóa tất cả dữ liệu shop từ cache và PlayerPrefs.
    /// </summary>
    public static void ClearAllData()
    {
        CachedUserId = null;
        CachedShopSettings = null;
        GlobalAppConfig = null; // Xóa cả GlobalAppConfig khi đăng xuất

        // PlayerPrefs.DeleteAll(); // Cần cẩn thận khi dùng DeleteAll nếu bạn có lưu các setting khác không liên quan đến user
                                // Nếu dùng DeleteAll, không cần xóa từng key nữa:
                                // PlayerPrefs.DeleteKey("CachedUserId");
                                // ... (các key khác)

        // Để an toàn hơn, hãy xóa từng key liên quan đến session của ShopBizmate
        // nếu bạn có những PlayerPrefs key khác không muốn bị xóa.
        // Hoặc chỉ xóa những key đã set trong SaveShopDataToPlayerPrefs.
        PlayerPrefs.DeleteKey("CachedUserId");
        PlayerPrefs.DeleteKey("ShopName");
        PlayerPrefs.DeleteKey("PhoneNumber");
        PlayerPrefs.DeleteKey("TaxId");
        PlayerPrefs.DeleteKey("Industry");
        PlayerPrefs.DeleteKey("EInvoiceProvider");
        PlayerPrefs.DeleteKey("EInvoiceUser");
        PlayerPrefs.DeleteKey("EInvoicePass"); // Mặc dù không nên lưu, nhưng nếu đã lưu thì xóa
        PlayerPrefs.DeleteKey("InvoiceSerial");
        PlayerPrefs.DeleteKey("InvoiceForm");
        PlayerPrefs.DeleteKey("InvoiceType");
        PlayerPrefs.DeleteKey("FptAccessToken");
        PlayerPrefs.DeleteKey("FptTokenExpiryTime");
        PlayerPrefs.DeleteKey("PackageType"); // Xóa key mới
        PlayerPrefs.DeleteKey("LicenseEndDate"); // Xóa key cuối cùng
        // Nếu có key nào khác mà AuthManager hay ShopSettingManager set, hãy thêm vào đây.

        PlayerPrefs.Save();
        Debug.Log("ShopSessionData: Cleared all cached data and PlayerPrefs.");
    }
}