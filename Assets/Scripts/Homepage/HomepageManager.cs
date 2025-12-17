// File: Scripts/Homepage/HomepageManager.cs
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
using System.Collections.Generic; // Cần thêm để dùng List<>

public class HomepageManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI dailyRevenueText;
    public TextMeshProUGUI dailyOrderCountText;
    public Button logoutButton;
    
    // Khai báo thêm đối tượng cần di chuyển (Giữ nguyên của bạn)
    public RectTransform objectToMove; 

    [Header("UI Settings")]
    [Tooltip("Khoảng cách di chuyển lên trên của UI khi banner quảng cáo xuất hiện, tính bằng Pixel.")]
    public float moveDistanceInPixels = 200f;

    // Các nút điều hướng
    [Header("Navigation Buttons")]
    public Button inventoryButton;
    public Button salesButton;
    public Button invoiceButton;
    public Button reportButton;
    public Button shopSettingButton;

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private FirebaseUser currentUser;
    private float initialYPosition;

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
        
        // Hủy đăng ký AdMob
        if (AdManager.Instance != null)
        {
            AdManager.OnBannerLoaded -= OnBannerLoaded;
        }

        // Hủy đăng ký SalesDataService (PHẦN SỬA LỖI)
        if (SalesDataService.Instance != null)
        {
            SalesDataService.Instance.OnSalesLoaded -= UpdateDailyStats;
        }
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

        // Gán listener cho các nút điều hướng (Giữ nguyên logic của bạn)
        if (inventoryButton != null) inventoryButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Inventory", AppFeature.Inventory));
        if (salesButton != null) salesButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Sales", AppFeature.Sales));
        if (invoiceButton != null) invoiceButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Invoice", AppFeature.EInvoice));
        if (reportButton != null) reportButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("Report", AppFeature.Reports));
        if (shopSettingButton != null) shopSettingButton.onClick.AddListener(() => LoadSceneOrShowAccessDenied("ShopSetting", AppFeature.None));

        // Load dữ liệu ban đầu
        LoadShopName();
        CheckFeatureAccess();

        // --- PHẦN SỬA LỖI DATA FLOW: Lắng nghe SalesDataService thay vì gọi trực tiếp Firestore ---
        if (SalesDataService.Instance != null)
        {
            // Đăng ký sự kiện để khi bán hàng xong quay lại đây nó tự cập nhật
            SalesDataService.Instance.OnSalesLoaded -= UpdateDailyStats;
            SalesDataService.Instance.OnSalesLoaded += UpdateDailyStats;

            // Yêu cầu tải dữ liệu (Local hoặc Cloud tùy gói)
            SalesDataService.Instance.LoadAndListenForSales();
        }
        else
        {
            Debug.LogError("HomepageManager: SalesDataService chưa được khởi tạo (Instance null).");
        }
        // -----------------------------------------------------------------------------------------

        // Logic AdMob (Giữ nguyên của bạn)
        if (AdManager.Instance != null)
        {
            AdManager.OnBannerLoaded += OnBannerLoaded;
            AdManager.Instance.RequestAdaptiveBanner();
        }

        // Lưu vị trí Y ban đầu
        if (objectToMove != null)
        {
            initialYPosition = objectToMove.anchoredPosition.y;
        }
    }

    // --- PHẦN LOGIC MỚI: Xử lý dữ liệu từ SalesDataService ---
    private void UpdateDailyStats(List<SaleData> allSales)
    {
        if (dailyRevenueText == null || dailyOrderCountText == null) return;
        if (allSales == null) return;

        // Tính toán doanh thu hôm nay (Local Time)
        DateTime today = DateTime.Now.Date;
        var todaySales = allSales.Where(s => s.saleDate.ToDateTime().ToLocalTime().Date == today).ToList();

        long totalRevenue = todaySales.Sum(s => s.totalAmount);
        int orderCount = todaySales.Count;

        // Cập nhật UI
        dailyRevenueText.text = $" {totalRevenue:N0} VNĐ";
        dailyOrderCountText.text = $" {orderCount}";

        Debug.Log($"HomepageManager: Đã cập nhật doanh thu ngày từ SalesDataService. {orderCount} đơn - {totalRevenue} VNĐ");
    }
    // ---------------------------------------------------------

    // Hàm này giữ nguyên logic di chuyển UI của bạn
    private void OnBannerLoaded(float bannerHeightInDips)
    {
        if (objectToMove != null)
        {
            float fixedMoveDistanceInPixels = moveDistanceInPixels;
            CanvasScaler scaler = objectToMove.GetComponentInParent<Canvas>().GetComponent<CanvasScaler>();
            float referenceResolutionY = scaler.referenceResolution.y;
            float heightRatio = referenceResolutionY / Screen.height;
            float fixedMoveDistanceInCanvasUnits = fixedMoveDistanceInPixels * heightRatio;

            objectToMove.anchoredPosition = new Vector2(objectToMove.anchoredPosition.x, initialYPosition + fixedMoveDistanceInCanvasUnits);
            Debug.Log($"HomepageManager: Đã di chuyển đối tượng '{objectToMove.name}' lên trên {fixedMoveDistanceInCanvasUnits} đơn vị.");
        }
    }

    private void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != currentUser)
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            bool signedIn = currentUser != null;
            if (signedIn)
            {
                Debug.Log($"Người dùng đã đăng nhập: {currentUser.DisplayName ?? currentUser.Email}");
                CheckFeatureAccess();
                LoadShopName();
                // Khi đăng nhập lại, trigger load lại sales
                if (SalesDataService.Instance != null) SalesDataService.Instance.LoadAndListenForSales();
            }
            else
            {
                // Reset UI khi đăng xuất
                shopNameText.text = "(Chưa đăng nhập)";
                dailyRevenueText.text = " 0 VNĐ";
                dailyOrderCountText.text = " 0";
            }
        }
    }

    // Giữ nguyên logic CheckFeatureAccess của bạn
    private void CheckFeatureAccess()
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;

        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null)
        {
            Debug.LogWarning("HomepageManager: Cấu hình gói hoặc App Package Config chưa được tải.");
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
        if (requiredFeature != AppFeature.None)
        {
            hasAccess = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, requiredFeature);
        }
        button.interactable = hasAccess;
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
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    private async void LoadShopName()
    {
        if (shopNameText == null) return;

        // Ưu tiên lấy từ Cache
        if (CachedShopSettings != null && !string.IsNullOrEmpty(CachedShopSettings.shopName))
        {
            shopNameText.text = $" {CachedShopSettings.shopName}";
            return;
        }

        if (currentUser == null) return;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            shopNameText.text = " (Offline)";
            return;
        }

        try
        {
            DocumentSnapshot snapshot = await db.Collection("shops").Document(currentUser.UserId).GetSnapshotAsync();
            if (snapshot.Exists && snapshot.ContainsField("shopName"))
            {
                string name = snapshot.GetValue<string>("shopName");
                shopNameText.text = $"{name}";
                
                // Cập nhật cache
                if (ShopSessionData.CachedShopSettings == null)
                {
                    ShopSessionData.SetCachedShopSettings(currentUser.UserId, new ShopSettingManager.ShopData());
                }
                ShopSessionData.CachedShopSettings.shopName = name;
            }
            else
            {
                shopNameText.text = "Tên Shop: (Chưa thiết lập)";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi tải tên shop: {e.Message}");
            shopNameText.text = "Tên Shop: (Lỗi)";
        }
    }

    // Đã bỏ hàm LoadDailySalesData cũ vì đã thay bằng UpdateDailyStats

    public void OnLogoutButtonClicked()
    {
        if (AuthManager.Instance != null) AuthManager.Instance.SignOutAndReturnToLogin();
    }

    public void ShowUnderDevelopmentPopup()
    {
        StatusPopupManager.Instance.ShowPopup("Tính năng đang được phát triển.");
    }
}
