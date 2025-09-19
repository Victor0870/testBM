using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Threading.Tasks;
using static ShopSessionData;
using System.Linq;

public class HomepageManager : MonoBehaviour
{

    [Header("UI References")]
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI dailyRevenueText;
    public TextMeshProUGUI dailyOrderCountText;
    public Button logoutButton;

    // Các nút điều hướng đến các Scene/tính năng khác
    [Header("Navigation Buttons")]
    public Button inventoryButton;
    public Button salesButton;
    public Button invoiceButton;
    public Button reportButton;
    public Button shopSettingButton;

    // Các nút "đang phát triển" mà bạn gán thủ công từ Inspector
    [Header("Under Development Buttons")]
    // (Không cần khai báo ở đây nếu bạn gán hàm ShowUnderDevelopmentPopup() trực tiếp từ Inspector)

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private FirebaseUser currentUser;

    void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;


    }

    void Start()
    {
        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);
        }
        else
        {
            Debug.LogError("HomepageManager: Logout Button chưa được gán trong Inspector.");
        }

        // Gán listener cho các nút điều hướng
        if (inventoryButton != null) inventoryButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Inventory", AppFeature.Inventory));
        if (salesButton != null) salesButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Sales", AppFeature.Sales));
        if (invoiceButton != null) invoiceButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Invoice", AppFeature.EInvoice));
        if (reportButton != null) reportButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Report", AppFeature.Reports));
        if (shopSettingButton != null) shopSettingButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("ShopSetting", AppFeature.None));


        LoadHomepageData();
        CheckFeatureAccess();
        AdManager.Instance.RequestAdaptiveBanner();

    }

    private void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != currentUser)
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            bool signedIn = currentUser != null;
            if (!signedIn && currentUser == null)
            {
                Debug.Log("Người dùng đã đăng xuất.");
            }
            if (signedIn)
            {
                Debug.Log($"Người dùng đã đăng nhập: {currentUser.DisplayName ?? currentUser.Email}");
            }
            CheckFeatureAccess();
            LoadHomepageData();
        }
    }

    private void LoadHomepageData()
    {
        if (currentUser == null)
        {
            shopNameText.text = "(Chưa đăng nhập)";
            dailyRevenueText.text = " 0 VNĐ";
            dailyOrderCountText.text = " 0";
            return;
        }

        LoadShopName();
        LoadDailySalesData();
    }

    private void CheckFeatureAccess()
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;

        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null)
        {
            Debug.LogWarning("HomepageManager: Cấu hình gói hoặc App Package Config chưa được tải. Vô hiệu hóa các tính năng.");
            SetNavigationButtonsInteractable(false, AppFeature.None, "Không thể tải cấu hình gói ứng dụng.");
            return;
        }

        SetNavigationButtonAccess(salesButton, AppFeature.Sales, currentPackageName);
        SetNavigationButtonAccess(inventoryButton, AppFeature.Inventory, currentPackageName);
        SetNavigationButtonAccess(invoiceButton, AppFeature.EInvoice, currentPackageName);
        SetNavigationButtonAccess(reportButton, AppFeature.Reports, currentPackageName);
        SetNavigationButtonAccess(shopSettingButton, AppFeature.None, currentPackageName);
    }

    private void SetNavigationButtonAccess(Button button, AppFeature requiredFeature, string currentPackageName)
    {
        if (button == null) return;

        bool hasAccess = true;
        string accessDeniedMessage = "";

        if (requiredFeature != AppFeature.None)
        {
            hasAccess = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, requiredFeature);
            if (!hasAccess)
            {
                accessDeniedMessage = $"Tính năng '{requiredFeature.ToString()}' yêu cầu gói '{currentPackageName}' phù hợp. Vui lòng nâng cấp gói.";
            }
        }

        button.interactable = hasAccess;
        if (!hasAccess)
        {
            Debug.Log($"HomepageManager: Nút '{button.name}' bị vô hiệu hóa. {accessDeniedMessage}");
        }
    }

    private void SetNavigationButtonsInteractable(bool interactable, AppFeature requiredFeature = AppFeature.None, string message = null)
    {
        SetNavigationButtonAccess(salesButton, requiredFeature, ShopSessionData.CachedShopSettings?.packageType);
        SetNavigationButtonAccess(inventoryButton, requiredFeature, ShopSessionData.CachedShopSettings?.packageType);
        SetNavigationButtonAccess(invoiceButton, requiredFeature, ShopSessionData.CachedShopSettings?.packageType);
        SetNavigationButtonAccess(reportButton, requiredFeature, ShopSessionData.CachedShopSettings?.packageType);
        SetNavigationButtonAccess(shopSettingButton, AppFeature.None, ShopSessionData.CachedShopSettings?.packageType);
    }


    private void LoadSceneOrShowAccessDenied(string sceneName, AppFeature requiredFeature)
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;

        if (requiredFeature != AppFeature.None &&
            (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null ||
             !ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, requiredFeature)))
        {
            string featureName = requiredFeature.ToString();
            string msg = $"Tính năng '{featureName}' yêu cầu gói '{currentPackageName}' phù hợp. Vui lòng nâng cấp gói để sử dụng.";
            StatusPopupManager.Instance.ShowPopup(msg);
            Debug.LogWarning($"HomepageManager: Chặn truy cập Scene '{sceneName}'. {msg}");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            Debug.Log($"Chuyển sang scene: {sceneName}");
        }
    }


    private async void LoadShopName()
    {
        if (shopNameText == null)
        {
            Debug.LogError("HomepageManager: Shop Name Text (TextMeshProUGUI) chưa được gán.");
            return;
        }

        if (CachedShopSettings != null && !string.IsNullOrEmpty(CachedShopSettings.shopName))
        {
            shopNameText.text = $" {CachedShopSettings.shopName}";
            Debug.Log("HomepageManager: Tên Shop được tải từ cache.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Không thể tải tên shop.");
            shopNameText.text = " (Mất kết nối)";
            return;
        }

        string userId = currentUser.UserId;
        DocumentReference shopDocRef = db.Collection("shops").Document(userId);

        try
        {
            DocumentSnapshot snapshot = await shopDocRef.GetSnapshotAsync();
            if (snapshot.Exists && snapshot.ContainsField("shopName"))
            {
                string name = snapshot.GetValue<string>("shopName");
                shopNameText.text = $"{name}";
                Debug.Log($"HomepageManager: Tên Shop '{name}' tải từ Firestore.");

                // Sửa lỗi CS0272: The property or indexer 'ShopSessionData.CachedShopSettings' cannot be used in this context because the set accessor is inaccessible
                ShopSettingManager.ShopData tempShopData = ShopSessionData.CachedShopSettings;
                if (tempShopData == null)
                {
                    tempShopData = new ShopSettingManager.ShopData();
                }
                tempShopData.shopName = name;

                ShopSessionData.SetCachedShopSettings(userId, tempShopData);
            }
            else
            {
                shopNameText.text = "Tên Shop: (Chưa thiết lập)";
                StatusPopupManager.Instance.ShowPopup("Vui lòng thiết lập tên shop của bạn trong phần Cài đặt Shop.");
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi tải tên shop: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi tải tên shop.";
            }
            Debug.LogError(errorMessage);
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            shopNameText.text = "Tên Shop: (Lỗi)";
        }
    }

    private async void LoadDailySalesData()
    {
        if (dailyRevenueText == null || dailyOrderCountText == null)
        {
            Debug.LogError("HomepageManager: Daily Revenue/Order Count Text (TextMeshProUGUI) chưa được gán.");
            return;
        }

        if (currentUser == null)
        {
            dailyRevenueText.text = "  0 VNĐ";
            dailyOrderCountText.text = " 0";
            return;
        }

        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null ||
            !ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales))
        {
            dailyRevenueText.text = "  (Yêu cầu gói)";
            dailyOrderCountText.text = " (Yêu cầu gói)";
            Debug.Log($"HomepageManager: Gói '{currentPackageName}' không có quyền tải dữ liệu doanh thu.");
            return;
        }


        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Không thể tải dữ liệu bán hàng.");
            dailyRevenueText.text = "  (Mất kết nối)";
            dailyOrderCountText.text = " (Mất kết nối)";
            return;
        }

        string userId = currentUser.UserId;
        CollectionReference salesRef = db.Collection("shops").Document(userId).Collection("sales");

        DateTime todayUtc = DateTime.UtcNow.Date;
        DateTime tomorrowUtc = todayUtc.AddDays(1);

        Timestamp startOfDay = Timestamp.FromDateTime(todayUtc);
        Timestamp endOfDay = Timestamp.FromDateTime(tomorrowUtc);

        try
        {
            QuerySnapshot querySnapshot = await salesRef
                .WhereGreaterThanOrEqualTo("saleDate", startOfDay)
                .WhereLessThan("saleDate", endOfDay)
                .GetSnapshotAsync();

            long totalRevenue = 0;
            int orderCount = 0;

            foreach (DocumentSnapshot document in querySnapshot.Documents)
            {
                if (document.Exists)
                {
                    SaleData sale = document.ConvertTo<SaleData>();
                    totalRevenue += sale.totalAmount;
                    orderCount++;
                }
            }

            dailyRevenueText.text = $" {totalRevenue:N0} VNĐ";
            dailyOrderCountText.text = $" {orderCount}";
            Debug.Log($"HomepageManager: Đã tải dữ liệu bán hàng cho hôm nay: {orderCount} đơn, {totalRevenue} VNĐ.");
        }
        catch (Exception e)
        {
            string errorMessage = $"Lỗi tải dữ liệu bán hàng: {e.Message}";
            if (e is Firebase.FirebaseException firebaseEx && firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
            {
                errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi tải dữ liệu bán hàng.";
            }
            Debug.LogError(errorMessage);
            StatusPopupManager.Instance.ShowPopup(errorMessage);
            dailyRevenueText.text = " (Lỗi)";
            dailyOrderCountText.text = "(Lỗi)";
        }
    }

    public void OnLogoutButtonClicked()
    {
        Debug.Log("HomepageManager: Nút Thoát Ứng dụng được nhấn.");
        Application.Quit();
    }

    public void ShowUnderDevelopmentPopup()
    {
        StatusPopupManager.Instance.ShowPopup("Tính năng đang được phát triển.");
        Debug.Log("Tính năng đang được phát triển.");
    }
}