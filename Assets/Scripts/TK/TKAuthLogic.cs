// File: Scripts/TK/TKAuthLogic.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using static ShopSessionData;
using static ShopSettingManager;
using System.Linq;

public class TKAuthLogic : MonoBehaviour
{
    // Singleton Instance
    public static TKAuthLogic Instance { get; private set; }

    // Constants từ AuthManager.cs (cũ)
    private const string SHOPS_COLLECTION = "shops";
    private const string APP_SETTINGS_COLLECTION = "app_settings";
    private const string PACKAGE_COSTS_DOC_ID = "package_costs";
    private const string ENTER_SHOP_SETTING_EDIT_MODE_KEY = "EnterShopSettingEditMode";

    // Firebase Core
    private FirebaseAuth auth;
    private FirebaseUser user;
    private FirebaseFirestore db;
    private bool _isFirebaseInitialized = false;

    // Delegate để TKAuthManager có thể lắng nghe kết quả
    public delegate void AuthResultDelegate(bool success, string statusMessage);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }
    }

    void Start()
    {
        // Khởi tạo Firebase khi component này được tạo (ngay cả khi chuyển Scene)
        InitializeFirebaseAsync();
    }

    // Đảm bảo Firebase được khởi tạo và kiểm tra trạng thái đăng nhập
    public async Task InitializeFirebaseAsync()
    {
        if (_isFirebaseInitialized) return;

        var dependencyStatus = DependencyStatus.UnavailableOther;
        try
        {
            dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Firebase] Lỗi khi kiểm tra phụ thuộc: {e.Message}");
            return;
        }

        if (dependencyStatus == DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            auth.StateChanged -= AuthStateChanged;
            auth.StateChanged += AuthStateChanged;
            _isFirebaseInitialized = true;
            Debug.Log("TKAuthLogic: Firebase Core Initialized.");

            await ProcessLoginState();
        }
        else
        {
            Debug.LogError($"[Firebase] Lỗi khởi tạo: {dependencyStatus}");
        }
    }

    private void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(async () => {
            FirebaseUser currentFirebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentFirebaseUser != user)
            {
                if (user != null && currentFirebaseUser == null)
                {
                    Debug.Log("TKAuthLogic: Người dùng đã đăng xuất. Đang chuyển về TKLogin.");
                    ShopSessionData.ClearAllData();
                    AuthSession.ComingFromLogout = true;
                    SceneManager.LoadScene("TKLogin");
                }
                user = currentFirebaseUser;
                // Nếu user vừa đăng nhập thành công (khi AuthStateChanged được kích hoạt),
                // ta cần ProcessLoginState để tải dữ liệu shop và chuyển hướng.
                if (user != null)
                {
                    await ProcessLoginState();
                }
            }
        });
    }

    private async Task ProcessLoginState()
    {
        // Logic ProcessLoginState (giữ nguyên logic từ AuthManger.cs, loại bỏ UI cũ)
        if (AuthSession.ComingFromLogout)
        {
            if (auth != null && auth.CurrentUser != null)
            {
                auth.SignOut();
                user = null;
            }
            AuthSession.ComingFromLogout = false;
            await Task.Yield();
            return;
        }

        user = auth.CurrentUser;

        if (user == null) return;

        // BẮT ĐẦU TẢI CẤU HÌNH VÀ DỮ LIỆU SESSION.
        await LoadGlobalAppConfig();
        bool loadedFromPlayerPrefsSuccessfully = ShopSessionData.LoadFromPlayerPrefs();

        if (loadedFromPlayerPrefsSuccessfully && ShopSessionData.CachedUserId == user.UserId)
        {
            await PerformInitialRedirection(ShopSessionData.CachedShopSettings);
        }
        else
        {
            await LoadAndCacheShopData(user.UserId);
            await PerformInitialRedirection(ShopSessionData.CachedShopSettings);
        }
    }

    private async Task LoadGlobalAppConfig()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        try
        {
            DocumentReference configRef = db.Collection(APP_SETTINGS_COLLECTION).Document(PACKAGE_COSTS_DOC_ID);
            DocumentSnapshot snapshot = await configRef.GetSnapshotAsync();
            if (snapshot.Exists)
            {
                ShopSessionData.SetGlobalAppConfig(snapshot.ConvertTo<GlobalAppConfigData>());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"TKAuthLogic: Lỗi tải cấu hình gói: {e.Message}");
        }
    }

    private async Task LoadAndCacheShopData(string userId)
    {
        DocumentReference shopRef = db.Collection(SHOPS_COLLECTION).Document(userId);
        Task<DocumentSnapshot> shopTask = shopRef.GetSnapshotAsync();
        await shopTask;

        DocumentSnapshot shopSnapshot = shopTask.Result;
        ShopData shopData = null;

        if (shopSnapshot.Exists)
        {
             shopData = shopSnapshot.ConvertTo<ShopData>();
             if (string.IsNullOrEmpty(shopData.packageType)) shopData.packageType = "Basic";
             // Logic kiểm tra licenseEndDate và cập nhật nếu cần (giữ nguyên logic cũ)
        }
        else
        {
            long freeTrialDays = ShopSessionData.GlobalAppConfig?.FreeTrialDurationDays ?? 14;

            shopData = new ShopData {
                shopName = "",
                phoneNumber = "",
                taxId = "",
                industry = "Chọn Ngành hàng...",
                eInvoiceProvider = "Chọn Nhà cung cấp...",
                eInvoiceUser = "",
                eInvoicePass = "",
                invoiceSerial = "",
                invoiceForm = "Chọn Form...",
                invoiceType = "Chọn Type...",
                fptAccessToken = "",
                fptTokenExpiryTime = 0,
                licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(freeTrialDays)),
                packageType = "Pro"
            };
            await shopRef.SetAsync(shopData, SetOptions.MergeAll);
        }
        ShopSessionData.SetCachedShopSettings(userId, shopData);
    }

    private async Task PerformInitialRedirection(ShopData currentShopData)
    {
        if (user == null) return;

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (string.IsNullOrEmpty(currentShopData.shopName) ||
                (currentShopData.licenseEndDate != null && currentShopData.licenseEndDate.ToDateTime() < DateTime.UtcNow))
            {
                PlayerPrefs.SetInt(ENTER_SHOP_SETTING_EDIT_MODE_KEY, 1);
                PlayerPrefs.Save();
                SceneManager.LoadScene("TKShopSetting");
                return;
            }

            PlayerPrefs.DeleteKey(ENTER_SHOP_SETTING_EDIT_MODE_KEY);
            PlayerPrefs.Save();
            SceneManager.LoadScene("TKHomepage");
        });
        await Task.CompletedTask;
    }

    // =========================================================================
    // CÁC HÀM PUBLIC MỚI ĐỂ TKAuthManager GỌI
    // =========================================================================

    public async void LoginUserWithCredentials(string email, string password, AuthResultDelegate callback)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            callback(false, "Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        try
        {
            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser loggedInUser = result.User;

            await loggedInUser.ReloadAsync();

            if (!loggedInUser.IsEmailVerified)
            {
                auth.SignOut();
                callback(false, "Email của bạn chưa được xác minh. Vui lòng kiểm tra hộp thư đến (bao gồm cả thư mục Spam/Junk) để tìm email xác minh và nhấp vào liên kết.");
                return;
            }

            user = loggedInUser;
            await LoadGlobalAppConfig();
            await LoadAndCacheShopData(user.UserId);

            callback(true, "Đăng nhập thành công!");
            await PerformInitialRedirection(ShopSessionData.CachedShopSettings);
        }
        catch (FirebaseException firebaseEx)
        {
            AuthError authError = (AuthError)firebaseEx.ErrorCode;
            string errorMessage;

            // Sao chép logic switch/case từ AuthManager.LoginUser
            switch (authError)
            {
                case AuthError.UserNotFound:
                case AuthError.WrongPassword:
                    errorMessage = "Email hoặc mật khẩu không đúng.";
                    break;
                case AuthError.NetworkRequestFailed:
                    errorMessage = "Không có kết nối Internet hoặc lỗi mạng khi đăng nhập. Vui lòng kiểm tra mạng.";
                    break;
                case AuthError.UserDisabled:
                    errorMessage = "Tài khoản của bạn đã bị vô hiệu hóa.";
                    break;
                default:
                    errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                    break;
            }

            callback(false, errorMessage);
            auth.SignOut();
        }
        catch (Exception)
        {
            callback(false, "Đã xảy ra lỗi không mong muốn khi đăng nhập.");
        }
    }

    public async void RegisterUserWithCredentials(string email, string password, string username, AuthResultDelegate callback)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            callback(false, "Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        try
        {
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = result.User;

            await UpdateUserProfile(newUser, username);
            await newUser.SendEmailVerificationAsync();

            DocumentReference shopRef = db.Collection(SHOPS_COLLECTION).Document(newUser.UserId);
            long freeTrialDays = ShopSessionData.GlobalAppConfig?.FreeTrialDurationDays ?? 14;
            ShopData defaultShopData = new ShopData {
                shopName = "",
                phoneNumber = "",
                taxId = "",
                industry = "Chọn Ngành hàng...",
                eInvoiceProvider = "Chọn Nhà cung cấp...",
                eInvoiceUser = "",
                eInvoicePass = "",
                invoiceSerial = "",
                invoiceForm = "Chọn Form...",
                invoiceType = "Chọn Type...",
                fptAccessToken = "",
                fptTokenExpiryTime = 0,
                licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(freeTrialDays)),
                packageType = "Pro"
            };
            await shopRef.SetAsync(defaultShopData, SetOptions.MergeAll);

            auth.SignOut();
            callback(true, "Tạo tài khoản thành công! Vui lòng kiểm tra email để xác minh trước khi đăng nhập.");

        }
        catch (FirebaseException firebaseEx)
        {
            // Sao chép logic bắt lỗi từ AuthManager.RegisterUser
            AuthError authError = (AuthError)firebaseEx.ErrorCode;
            string errorMessage = "Tạo tài khoản thất bại.";

            switch (authError)
            {
                case AuthError.EmailAlreadyInUse:
                    errorMessage = "Email này đã được sử dụng. Vui lòng thử email khác hoặc đăng nhập.";
                    break;
                case AuthError.WeakPassword:
                    errorMessage = "Mật khẩu quá yếu. Firebase yêu cầu tối thiểu 6 ký tự. Vui lòng đặt mật khẩu mạnh hơn.";
                    break;
                case AuthError.InvalidEmail:
                    errorMessage = "Địa chỉ email không hợp lệ.";
                    break;
                default:
                    errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                    break;
            }
            callback(false, errorMessage);
        }
        catch (Exception)
        {
            callback(false, "Đã xảy ra lỗi không mong muốn khi tạo tài khoản.");
        }
    }

    private async Task UpdateUserProfile(FirebaseUser userProfileToUpdate, string displayName)
    {
        UserProfile profile = new UserProfile { DisplayName = displayName };
        try
        {
            await userProfileToUpdate.UpdateUserProfileAsync(profile);
        }
        catch (Exception e)
        {
            Debug.LogError("Cập nhật hồ sơ người dùng lỗi: " + e.Message);
        }
    }

    public void SignOutAndReturnToLogin()
    {
        AuthSession.ComingFromLogout = true;
        ShopSessionData.ClearAllData();
        if (auth != null && user != null)
        {
             auth.SignOut();
             user = null;
        }
        // AuthStateChanged sẽ kích hoạt chuyển Scene về TKLogin
    }

    public bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}