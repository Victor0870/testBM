using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using static ShopSessionData;
using static ShopSettingManager; // Cần cho ShopData class
using System.Linq;

public class AuthManager : MonoBehaviour
{
    // Các trường InputField và Text từ giao diện người dùng Unity
    public Button loginButton; // Nút Đăng nhập
    public Button createAccountButton; // Nút Tạo tài khoản
    [Header("Create Account UI")]
    public TMP_InputField usernameInputField;
    public TMP_InputField emailInputField;
    public TMP_InputField passwordInputField;
    public TMP_InputField confirmPasswordInputField;

    [Header("Login UI")]
    public TMP_InputField loginEmailInputField;
    public TMP_InputField loginPasswordInputField;

    [Header("Common UI Elements")]
    public TMP_Text statusText;
    public GameObject loadingPanel;
    public GameObject loginPanel;
    public GameObject createAccountPanel;
    public GameObject emailVerificationNotificationPanel;

    [Header("Global UI Prefabs")]
    public GameObject fullScreenLoadingPrefab; // Kéo Prefab Loading Panel toàn màn hình vào đây
    private GameObject _activeFullScreenLoadingPanel = null;

    // Khai báo một biến public để kéo Asset PackageConfig vào từ Inspector (cho mục đích demo/test)
    [Header("App Configuration")]
    public PackageConfig appPackageConfigAsset; // Kéo Asset PackageConfig đã tạo vào đây

    private FirebaseAuth auth;
    private FirebaseUser user;
    private FirebaseFirestore db;

    // Biến tĩnh để giữ cấu hình gói toàn cầu
    public static GlobalAppConfigData GlobalAppConfig { get; private set; } // THÊM DÒNG NÀY

    // Khai báo một hằng số cho key trong PlayerPrefs
    private const string ENTER_SHOP_SETTING_EDIT_MODE_KEY = "EnterShopSettingEditMode"; // Đổi tên key

    // Constants cho Firestore
    private const string SHOPS_COLLECTION = "shops";
    private const string APP_SETTINGS_COLLECTION = "app_settings"; // THÊM DÒNG NÀY
    private const string PACKAGE_COSTS_DOC_ID = "package_costs"; // THÊM DÒNG NÀY

    // Singleton Instance
    public static AuthManager Instance { get; private set; }
    public FirebaseUser CurrentUser
    {
        get { return user; }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log("AuthManager: Đã thiết lập DontDestroyOnLoad.");
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(this.gameObject);
            Debug.LogWarning("AuthManager: Đã có AuthManager tồn tại, hủy bản sao mới.");
            return;
        }

        UnityMainThreadDispatcher.Instance();
    }

    async void Start()
    {
        // Đảm bảo các panel UI được đặt trạng thái mặc định ban đầu
        if (loginPanel != null) loginPanel.SetActive(false);
        if (createAccountPanel != null) createAccountPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (emailVerificationNotificationPanel != null) emailVerificationNotificationPanel.SetActive(false);

        await PerformFirebaseInitializationAndLoginCheck();
    }


   private async Task PerformFirebaseInitializationAndLoginCheck()
       {
           // Hiển thị loadingPanel trong khi chờ Firebase khởi tạo
           if (loadingPanel != null) loadingPanel.SetActive(true);
           // Tắt tất cả các panel tương tác khác trong khi loading
           if (loginPanel != null) loginPanel.SetActive(false);
           if (createAccountPanel != null) createAccountPanel.SetActive(false);

           var dependencyStatus = DependencyStatus.UnavailableOther;
           try
           {
               dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
           }
           catch (Exception e)
           {
               Debug.LogError($"<color=red>[Firebase] Lỗi khi kiểm tra phụ thuộc: {e.Message}</color>");
               UnityMainThreadDispatcher.Instance().Enqueue(() => {
                   if (loadingPanel != null) loadingPanel.SetActive(false);
                   StatusPopupManager.Instance.ShowPopup("Lỗi khởi tạo Firebase! Vui lòng kiểm tra kết nối internet và thử lại.");
               });
               return;
           }


           if (dependencyStatus == DependencyStatus.Available)
           {
               Debug.Log("<color=green>[Firebase] Khởi tạo thành công.</color>");
               auth = FirebaseAuth.DefaultInstance;
               db = FirebaseFirestore.DefaultInstance;
               // Đăng ký StateChanged listener ở đây sau khi auth chắc chắn tồn tại
               auth.StateChanged -= AuthStateChanged;
               auth.StateChanged += AuthStateChanged;

               // ĐÃ BỎ: await LoadGlobalAppConfig(); // KHÔNG TẢI CẤU HÌNH GÓI SỚM NỮA!

               // Khởi tạo PackageConfig trong ShopSessionData nếu chưa có
               if (appPackageConfigAsset != null) // Đảm bảo bạn đã kéo Asset vào Inspector
               {
                   ShopSessionData.InitializePackageConfig(appPackageConfigAsset);
               }
               else
               {
                   Debug.LogError("AuthManager: App Package Config Asset chưa được gán trong Inspector!");
               }

               // CHỈ CẦN GỌI ProcessLoginState
               await ProcessLoginState();
           }
           else
           {
               Debug.LogError($"<color=red>[Firebase] Lỗi khởi tạo: {dependencyStatus}</color>");
               UnityMainThreadDispatcher.Instance().Enqueue(() => {
                   if (loadingPanel != null) loadingPanel.SetActive(false);
                   StatusPopupManager.Instance.ShowPopup("Không thể khởi tạo Firebase. Vui lòng kiểm tra cài đặt của bạn.");
               });
           }
       }

    // THÊM PHƯƠNG THỨC TẢI CẤU HÌNH GÓI TOÀN CẦU
     private async Task LoadGlobalAppConfig()
        {
            // Kiểm tra mạng ngay lập tức. Nếu mất mạng, hiển thị popup và dừng.
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Không thể tải cấu hình gói ứng dụng.");
                Debug.LogError("AuthManager: Cannot load global app config, no internet connection.");
                return;
            }

            try
            {
                DocumentReference configRef = db.Collection(APP_SETTINGS_COLLECTION).Document(PACKAGE_COSTS_DOC_ID);
                DocumentSnapshot snapshot = await configRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    GlobalAppConfig = snapshot.ConvertTo<GlobalAppConfigData>();
                    Debug.Log("AuthManager: Cấu hình gói đã được tải từ Firestore.");
                    // Tùy chọn: Đồng bộ GlobalAppConfig.Features vào PackageConfigAsset nếu bạn muốn
                    // nhưng nếu PackageConfigAsset chỉ là tham chiếu và GlobalAppConfig là nguồn chính, thì không cần.
                }
                else
                {
                    Debug.LogError($"AuthManager: Document '{PACKAGE_COSTS_DOC_ID}' không tồn tại trong collection '{APP_SETTINGS_COLLECTION}'. Vui lòng tạo thủ công trên Firestore.");
                    // Vẫn hiển thị popup cho lỗi cấu hình server nghiêm trọng này.
                    StatusPopupManager.Instance.ShowPopup("Lỗi: Cấu hình gói ứng dụng chưa được thiết lập trên server. Vui lòng liên hệ quản trị.");
                }
            }
            catch (Exception e)
            {
                string errorMessage = $"AuthManager: Lỗi tải cấu hình gói: {e.Message}";
                bool shouldShowPopup = true; // Mặc định là hiển thị popup

                if (e is Firebase.FirebaseException firebaseEx)
                {
                    if (firebaseEx.ErrorCode == (int)FirestoreError.Unavailable)
                    {
                        // Lỗi phổ biến nhất: mất mạng hoặc server tạm thời không khả dụng.
                        if (Application.internetReachability == NetworkReachability.NotReachable)
                        {
                            errorMessage = "Không có kết nối Internet hoặc máy chủ Firebase không khả dụng khi tải cấu hình gói. Vui lòng kiểm tra mạng của bạn.";
                        }
                        else
                        {
                            // Đây có thể là lỗi server tạm thời.
                            // Chúng ta KHÔNG hiển thị popup ngay lập tức để tránh làm gián đoạn người dùng mới,
                            // vì AuthManager vẫn có thể tiếp tục với giá trị GlobalAppConfig mặc định (null) hoặc mặc định được mã hóa.
                            errorMessage = $"Lỗi Firebase Server khi tải cấu hình gói: {firebaseEx.Message}. Sẽ sử dụng giá trị mặc định nếu có.";
                            shouldShowPopup = false; // QUAN TRỌNG: Ngăn hiển thị popup cho lỗi máy chủ tạm thời
                        }
                    }
                    else
                    {
                         // Các lỗi Firebase khác (ví dụ: Security Rule Denied) vẫn nên được thông báo.
                         errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                    }
                }

                Debug.LogError(errorMessage);

                if (shouldShowPopup)
                {
                    // Chỉ hiển thị popup nếu đó là lỗi nghiêm trọng (Document không tồn tại) hoặc mất mạng rõ ràng.
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        StatusPopupManager.Instance.ShowPopup(errorMessage);
                    });
                }
            }
        }


   private async Task ProcessLoginState()
   {
       // Bắt lỗi tổng thể cho toàn bộ quá trình async/await
       try
       {
           // 1. Luôn ưu tiên trạng thái ĐĂNG XUẤT (ComingFromLogout).
           if (AuthSession.ComingFromLogout) // Dùng AuthSession.cs
           {
               Debug.Log("AuthManager: [FIX] Phát hiện cờ đăng xuất. Đảm bảo SignOut lần cuối và hiển thị Login.");

               // Lớp Phòng thủ Cuối cùng (FIX RACE CONDITION): Buộc Firebase xóa sạch trạng thái
               if (auth != null && auth.CurrentUser != null)
               {
                   auth.SignOut();
                   user = null; // Reset biến user cục bộ (Theo gợi ý)
                   Debug.Log("AuthManager: Đã gọi auth.SignOut() lần cuối và reset user.");
               }

               AuthSession.ComingFromLogout = false; // Reset cờ
               if (loadingPanel != null) loadingPanel.SetActive(false);

               // Đợi 1 frame để UI và event system của Firebase/Unity ổn định (Theo gợi ý)
               await Task.Yield();
               ShowLoginPanel();
               return;
           }

           user = auth.CurrentUser;

           // 2. Kiểm tra trạng thái người dùng Firebase hiện tại.
           if (user == null)
           {
               // KHÔNG CÓ người dùng đang đăng nhập.
               Debug.Log("AuthManager: Không có người dùng nào đang đăng nhập. Hiển thị màn hình đăng nhập.");
               if (loadingPanel != null) loadingPanel.SetActive(false);
               ShowLoginPanel();
               return;
           }

           // 3. Đã có người dùng (user != null). BẮT ĐẦU TẢI CẤU HÌNH VÀ DỮ LIỆU SESSION.

           // ĐẢM BẢO TEXT VÀ PANEL XUẤT HIỆN TRƯỚC TÁC VỤ ASYNC ĐẦU TIÊN
           if (statusText != null) statusText.text = "Đang kiểm tra phiên đăng nhập...";
           if (loadingPanel != null) loadingPanel.SetActive(true);

           // LoadGlobalAppConfig() đã có try/catch bên trong
           await LoadGlobalAppConfig();

           bool loadedFromPlayerPrefsSuccessfully = ShopSessionData.LoadFromPlayerPrefs();

           if (loadedFromPlayerPrefsSuccessfully && ShopSessionData.CachedUserId == user.UserId)
           {
               Debug.Log($"AuthManager: Detected active Firebase user {user.Email} and VALID cached data. Using cached data for initial redirection.");
               await PerformInitialRedirection(ShopSessionData.CachedShopSettings);
           }
           else
           {
               // Cache không hợp lệ. Tải dữ liệu mới từ Firestore.
               Debug.Log($"AuthManager: Cached data invalid for user {user.Email}. Attempting to load fresh data from Firestore.");
               // LoadAndCacheShopData() đã có try/catch bên trong
               await LoadAndCacheShopData(user.UserId);
               await PerformInitialRedirection(ShopSessionData.CachedShopSettings);
           }
       }
       catch (Exception ex)
       {
           // Bắt lỗi nghiêm trọng nếu các hàm nội bộ không xử lý được
           Debug.LogError($"Lỗi nghiêm trọng trong ProcessLoginState: {ex.Message}");

           // Fallback: Đảm bảo người dùng luôn thấy màn hình login nếu có lỗi nghiêm trọng
           UnityMainThreadDispatcher.Instance().Enqueue(() => {
               StatusPopupManager.Instance.ShowPopup("Đã xảy ra lỗi nghiêm trọng khi tải dữ liệu. Vui lòng thử đăng nhập lại.");
               if (loadingPanel != null) loadingPanel.SetActive(false);
               ShowLoginPanel();
           });
       }
   }
   // Thêm hàm trợ giúp này vào AuthManager (ngoài phương thức OnSceneLoaded)
 private GameObject FindGOByNameInAll(string name)
 {
     // LƯU Ý: Cần có 'using System.Linq;' ở đầu file.
     // Dùng FindObjectsOfType<Transform>(true) để tìm kiếm tất cả GameObject (Active/Inactive)
     return FindObjectsOfType<Transform>(true)
            .FirstOrDefault(t => t.gameObject.name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))?.gameObject;
 }

   private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
   {
       if (scene.name == "Login")
       {
           Debug.Log("AuthManager: Bắt đầu khôi phục tham chiếu UI cho Scene Login mới.");

           // Khai báo các biến tạm thời để giữ kết quả tìm kiếm GameObject
           GameObject go;
           // TMP_InputField input; // Không cần thiết

           // ********** 1. KHÔI PHỤC GAME OBJECTS (PANEL) DÙNG HÀM TÌM ẨN **********
           loginPanel = FindGOByNameInAll("Light - Log In");
           createAccountPanel = FindGOByNameInAll("Light - Sign Up");
           loadingPanel = FindGOByNameInAll("LoadingPanelRoot");
           emailVerificationNotificationPanel = FindGOByNameInAll("Notice");

           // ********** 2. KHÔI PHỤC COMPONENTS DÙNG HÀM TÌM ẨN **********

           // Status Text
           go = FindGOByNameInAll("Text"); // Tên chính xác từ Inspector
           if (go != null)
           {
               statusText = go.GetComponent<TMP_Text>();
           }
           else
           {
                Debug.LogError("AuthManager: Lỗi khôi phục Status Text! Không tìm thấy đối tượng Status Text.");
           }


           // InputField cho Tạo tài khoản
           // LƯU Ý: Đang sử dụng tên placeholder để giữ cấu trúc. Vui lòng thay bằng tên chính xác nếu lỗi.
           go = FindGOByNameInAll("InputField - Username");
           if (go != null) usernameInputField = go.GetComponent<TMP_InputField>();
           else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy UsernameInputField.");

           go = FindGOByNameInAll("InputField - Email");
           if (go != null) emailInputField = go.GetComponent<TMP_InputField>();
           else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy EmailInputField.");

           go = FindGOByNameInAll("InputField - Password");
           if (go != null) passwordInputField = go.GetComponent<TMP_InputField>();
           else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy PasswordInputField.");

           go = FindGOByNameInAll("InputField - Password (1)");
           if (go != null) confirmPasswordInputField = go.GetComponent<TMP_InputField>();
          else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy ConfirmPasswordInputField.");

           // InputField cho Đăng nhập
           go = FindGOByNameInAll("InputField - Username 1");
           if (go != null) loginEmailInputField = go.GetComponent<TMP_InputField>();
           else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy LoginEmailInputField.");

           go = FindGOByNameInAll("InputField - Password 2");
           if (go != null) loginPasswordInputField = go.GetComponent<TMP_InputField>();
           else Debug.LogError("AuthManager: KHÔI PHỤC LỖI! Không tìm thấy LoginPasswordInputField.");

           Button FindButtonByNameInAll(string name)
                   {
                       return FindObjectsOfType<Button>(true)
                              .FirstOrDefault(b => b.gameObject.name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
                   }

                   // 1. Nút Đăng nhập Chính
                   Button loginButton = FindButtonByNameInAll("Button 2"); // Thay tên GO thực tế
                   if (loginButton != null)
                   {
                       loginButton.onClick.RemoveAllListeners();
                       loginButton.onClick.AddListener(OnLoginButtonClicked);
                   }

                   // 2. Nút Tạo tài khoản Chính
                   Button createAccountButton = FindButtonByNameInAll("Button"); // Thay tên GO thực tế
                   if (createAccountButton != null)
                   {
                       createAccountButton.onClick.RemoveAllListeners();
                       createAccountButton.onClick.AddListener(OnCreateAccountButtonClicked);
                   }

           // ********** KIỂM TRA LỖI SAU KHÔI PHỤC **********
           // Nếu bất kỳ biến quan trọng nào vẫn là null, chúng ta sẽ log lỗi
           if (loginPanel == null || loginEmailInputField == null)
           {
                Debug.LogError("AuthManager: Khôi phục thất bại! Kiểm tra lại tên GameObject và InputField trong Scene Login.");
           }

           Debug.Log("AuthManager: Khôi phục tham chiếu UI đã hoàn tất.");
       }
   }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            FirebaseUser currentFirebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentFirebaseUser != user)
            {
                if (user != null && currentFirebaseUser == null)
                {
                    Debug.Log("Người dùng đã đăng xuất. Xóa cache và hiển thị màn hình đăng nhập.");
                    ShopSessionData.ClearAllData();
                    AuthSession.ComingFromLogout = true;
                    ShowLoginPanel();
                }
                user = currentFirebaseUser;
            }
        });
    }

    private void ShowLoginPanel()
    {
        if (statusText != null) statusText.text = "";
        if (loginPanel != null) loginPanel.SetActive(true);
        if (createAccountPanel != null) createAccountPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (emailVerificationNotificationPanel != null) emailVerificationNotificationPanel.SetActive(false);
        SetPanelInteractable(loginPanel, true);

        if (loginEmailInputField != null) loginEmailInputField.text = "";
        if (loginPasswordInputField != null) loginPasswordInputField.text = "";
    }

    public void OnCreateAccountButtonClicked()
    {
        string username = usernameInputField.text.Trim();
        string email = emailInputField.text.Trim();
        string password = passwordInputField.text;
        string confirmPassword = confirmPasswordInputField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng điền đầy đủ Tên tài khoản, Email, Mật khẩu và Xác nhận mật khẩu.");
            return;
        }

        if (password.Length < 6)
        {
            StatusPopupManager.Instance.ShowPopup("Mật khẩu phải có ít nhất 6 ký tự.");
            return;
        }

        if (!IsValidEmail(email))
        {
            StatusPopupManager.Instance.ShowPopup("Địa chỉ email không hợp lệ. Vui lòng kiểm tra lại.");
            return;
        }

        if (password != confirmPassword)
        {
            StatusPopupManager.Instance.ShowPopup("Mật khẩu xác nhận không khớp. Vui lòng kiểm tra lại.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        if (statusText != null) statusText.text = "Đang tạo tài khoản...";
        if (loadingPanel != null) loadingPanel.SetActive(true);
        SetPanelInteractable(createAccountPanel, false);
        RegisterUser(email, password, username);
    }

    public void OnLoginPromptClicked()
    {
        Debug.Log("Chuyển sang màn hình Đăng nhập.");
        if (createAccountPanel != null) createAccountPanel.SetActive(false);
        ShowLoginPanel();
    }

    public void OnLoginPromptClickedToCreateAccount()
    {
        Debug.Log("Chuyển sang màn hình Tạo tài khoản.");
        if (loginPanel != null) loginPanel.SetActive(false);
        if (createAccountPanel != null) createAccountPanel.SetActive(true);
        if (statusText != null) statusText.text = "";
        if (usernameInputField != null) usernameInputField.text = "";
        if (emailInputField != null) emailInputField.text = "";
        if (passwordInputField != null) passwordInputField.text = "";
        if (confirmPasswordInputField != null) confirmPasswordInputField.text = "";
    }

    private void RegisterUser(string email, string password, string username)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(async task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync lỗi: " + task.Exception);
                Firebase.FirebaseException firebaseEx = task.Exception.GetBaseException() as Firebase.FirebaseException;
                string errorMessage = "Tạo tài khoản thất bại.";

                if (firebaseEx != null)
                {
                    AuthError authError = (AuthError)firebaseEx.ErrorCode;
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
                        case AuthError.NetworkRequestFailed:
                            errorMessage = "Không có kết nối Internet hoặc lỗi mạng. Vui lòng kiểm tra mạng và thử lại.";
                            break;
                        default:
                            errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                            break;
                    }
                }

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (loadingPanel != null) loadingPanel.SetActive(false);
                    SetPanelInteractable(createAccountPanel, true);
                    StatusPopupManager.Instance.ShowPopup(errorMessage);
                });
                return;
            }

            AuthResult result = task.Result;
            FirebaseUser newUser = result.User;
            Debug.Log("Tạo tài khoản thành công! UID: " + newUser.UserId);

            await UpdateUserProfile(newUser, username);
            await newUser.SendEmailVerificationAsync();

            DocumentReference shopRef = db.Collection(SHOPS_COLLECTION).Document(newUser.UserId);

            // Lấy thời gian dùng thử miễn phí từ cấu hình toàn cầu
            long freeTrialDays = GlobalAppConfig?.FreeTrialDurationDays ?? 14; // Mặc định 14 ngày nếu không tải được config

            ShopData defaultShopData = new ShopData
            {
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
                licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(freeTrialDays)), // SỬ DỤNG freeTrialDays
                packageType = "Pro" // MẶC ĐỊNH GÓI PRO CHO THỜI GIAN DÙNG THỬ
            };
            await shopRef.SetAsync(defaultShopData, SetOptions.MergeAll);
            Debug.Log($"Đã tạo shop document mặc định cho user mới {newUser.UserId}. License end date: {defaultShopData.licenseEndDate.ToDateTime():dd/MM/yyyy HH:mm}");


            auth.SignOut();

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (loadingPanel != null) loadingPanel.SetActive(false);
                if (createAccountPanel != null) createAccountPanel.SetActive(false);
                ShowLoginPanel();
                StatusPopupManager.Instance.ShowPopup(
                    "Tạo tài khoản thành công! Vui lòng kiểm tra email của bạn (bao gồm cả thư mục Spam/Junk) để xác minh tài khoản trước khi đăng nhập.",
                    () => {
                        AuthSession.ComingFromLogout = true;
                    }
                );
            });
        });
    }

    private void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (panel != null)
        {
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }

    private async Task UpdateUserProfile(FirebaseUser userProfileToUpdate, string displayName)
    {
        if (userProfileToUpdate == null) return;

        UserProfile profile = new UserProfile {
            DisplayName = displayName
        };

        try
        {
            await userProfileToUpdate.UpdateUserProfileAsync(profile);
            Debug.Log("Tên hiển thị đã được cập nhật thành công (trên luồng nền).");
        }
        catch (Exception e)
        {
            Debug.LogError("Cập nhật hồ sơ người dùng lỗi: " + e.Message);
        }
    }

    public void OnLoginButtonClicked()
    {
        string email = loginEmailInputField.text.Trim();
        string password = loginPasswordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            StatusPopupManager.Instance.ShowPopup("Vui lòng điền đầy đủ Email và Mật khẩu để đăng nhập.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }

        if (statusText != null) statusText.text = "Đang đăng nhập...";
        if (loadingPanel != null) loadingPanel.SetActive(true);
        SetPanelInteractable(loginPanel, false);
        LoginUser(email, password);
    }

    private async void LoginUser(string email, string password)
        {
            try
            {
                AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
                FirebaseUser loggedInUser = result.User;

                try
                {
                    await loggedInUser.ReloadAsync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Lỗi khi tải lại thông tin người dùng: {e.Message}");
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        if (loadingPanel != null) loadingPanel.SetActive(false);
                        SetPanelInteractable(loginPanel, true);
                        string reloadErrorMessage = "Lỗi khi kiểm tra trạng thái tài khoản. Vui lòng thử lại.";

                        if (e is Firebase.FirebaseException firebaseEx)
                        {
                            if (((AuthError)firebaseEx.ErrorCode) == AuthError.NetworkRequestFailed)
                            {
                                reloadErrorMessage = "Mất kết nối Internet khi kiểm tra tài khoản. Vui lòng thử lại.";
                            }
                            else
                            {
                                reloadErrorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                            }
                        }
                        StatusPopupManager.Instance.ShowPopup(reloadErrorMessage);
                    });
                    auth.SignOut();
                    return;
                }

                if (!loggedInUser.IsEmailVerified)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        if (loadingPanel != null) loadingPanel.SetActive(false);
                        SetPanelInteractable(loginPanel, true);
                        StatusPopupManager.Instance.ShowPopup(
                            "Email của bạn chưa được xác minh. Vui lòng kiểm tra hộp thư đến (bao gồm cả thư mục Spam/Junk) để tìm email xác minh và nhấp vào liên kết.",
                            () => {
                                auth.SignOut();
                            }
                        );
                    });
                    return;
                }

                Debug.LogFormat("Đăng nhập thành công: {0} ({1})",
                    loggedInUser.DisplayName ?? "N/A", loggedInUser.Email);

                user = loggedInUser;

                // ************ THAY ĐỔI TẠI ĐÂY ************
                // Đảm bảo cấu hình gói toàn cầu được tải (và kiểm tra mạng) trước khi tải dữ liệu shop.
                await LoadGlobalAppConfig();
                // ******************************************

                await LoadAndCacheShopData(user.UserId);

                await PerformInitialRedirection(ShopSessionData.CachedShopSettings);

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (loadingPanel != null) loadingPanel.SetActive(false);
                    SetPanelInteractable(loginPanel, true);
                });
            }
            catch (FirebaseException firebaseEx)
            {
                AuthError authError = (AuthError)firebaseEx.ErrorCode;
                string errorMessage = "Lỗi đăng nhập: Vui lòng thử lại.";

                switch (authError)
                {
                    case AuthError.UserNotFound:
                    case AuthError.WrongPassword:
                        errorMessage = "Email hoặc mật khẩu không đúng.";
                        break;
                    case AuthError.InvalidEmail:
                        errorMessage = "Địa chỉ email không hợp lệ.";
                        break;
                    case AuthError.UserDisabled:
                        errorMessage = "Tài khoản của bạn đã bị vô hiệu hóa.";
                        break;
                    case AuthError.TooManyRequests:
                        errorMessage = "Quá nhiều lần đăng nhập thất bại. Vui lòng thử lại sau.";
                        break;
                    case AuthError.NetworkRequestFailed:
                        errorMessage = "Không có kết nối Internet hoặc lỗi mạng khi đăng nhập. Vui lòng kiểm tra mạng.";
                        break;
                    default:
                        errorMessage = $"Lỗi Firebase: {firebaseEx.Message}";
                        break;
                }

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (loadingPanel != null) loadingPanel.SetActive(false);
                    SetPanelInteractable(loginPanel, true);
                    StatusPopupManager.Instance.ShowPopup(errorMessage);
                });
                Debug.LogError("Lỗi đăng nhập Firebase: " + firebaseEx.Message);
            }
            catch (Exception ex)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (loadingPanel != null) loadingPanel.SetActive(false);
                    SetPanelInteractable(loginPanel, true);
                    StatusPopupManager.Instance.ShowPopup("Đã xảy ra lỗi không mong muốn khi đăng nhập.");
                });
                Debug.LogError("Lỗi chung khi đăng nhập/kiểm tra shop: " + ex.Message);
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
            Debug.Log($"LoadAndCacheShopData: Shop document exists for {userId}. ShopName: {shopData.shopName ?? "N/A"}");

            if (shopData.licenseEndDate == null || shopData.licenseEndDate.ToDateTime() == DateTime.MinValue.ToUniversalTime())
            {
                Debug.LogWarning($"LoadAndCacheShopData: LicenseEndDate cho user {userId} bị thiếu hoặc không hợp lệ từ Firestore. Đặt lại thành mặc định 2 tuần.");
                shopData.licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(14));
            }
            // Đảm bảo packageType cũng được đặt giá trị mặc định nếu thiếu
            if (string.IsNullOrEmpty(shopData.packageType))
            {
                Debug.LogWarning($"LoadAndCacheShopData: PackageType cho user {userId} bị thiếu hoặc rỗng. Đặt lại thành mặc định 'Basic'.");
                shopData.packageType = "Basic";
            }

            Debug.Log($"LoadAndCacheShopData: Final LicenseEndDate for {userId}: {shopData.licenseEndDate.ToDateTime().ToLocalTime():dd/MM/yyyy HH:mm}");
            Debug.Log($"LoadAndCacheShopData: Final PackageType for {userId}: {shopData.packageType}");

            // Tùy chọn: Ghi lại giá trị đã sửa lỗi lên Firestore nếu bạn muốn đồng bộ
            // await shopRef.UpdateAsync(new Dictionary<string, object>
            // {
            //     {"licenseEndDate", shopData.licenseEndDate},
            //     {"packageType", shopData.packageType}
            // }, SetOptions.MergeAll);
        }
        else
        {
            Debug.Log($"LoadAndCacheShopData: Shop document for {userId} does not exist. Initializing with default values.");
            // Lấy thời gian dùng thử miễn phí từ cấu hình toàn cầu
            long freeTrialDays = GlobalAppConfig?.FreeTrialDurationDays ?? 14;

            shopData = new ShopData
            {
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
                licenseEndDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(freeTrialDays)), // SỬ DỤNG freeTrialDays
                packageType = "Pro" // MẶC ĐỊNH GÓI PRO CHO THỜI GIAN DÙNG THỬ
            };
            await shopRef.SetAsync(shopData, SetOptions.MergeAll);
            Debug.Log($"LoadAndCacheShopData: Đã tạo shop document mặc định cho user mới {userId}. License end date: {shopData.licenseEndDate.ToDateTime():dd/MM/yyyy HH:mm}");
        }
        ShopSessionData.SetCachedShopSettings(userId, shopData);
    }


    private async Task PerformInitialRedirection(ShopData currentShopData)
    {
        if (user == null)
        {
            Debug.LogError("PerformInitialRedirection: Member 'user' is null. Cannot perform redirection.");
            ShowLoginPanel();
            return;
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (string.IsNullOrEmpty(currentShopData.shopName))
            {
                Debug.Log($"Người dùng {user.UserId} chưa có tên shop. Chuyển đến ShopSetting (và vào edit mode).");
                PlayerPrefs.SetInt(ENTER_SHOP_SETTING_EDIT_MODE_KEY, 1);
                PlayerPrefs.Save();

                StatusPopupManager.Instance.ShowPopup("Vui lòng cấu hình thông tin shop của bạn trước khi tiếp tục.", () => {
                    SceneManager.LoadScene("ShopSetting");
                });
                return;
            }

            if (currentShopData.licenseEndDate == null || currentShopData.licenseEndDate.ToDateTime() < DateTime.UtcNow)
            {
                Debug.Log($"Người dùng {user.UserId} đã hết hạn sử dụng. Chuyển đến ShopSetting (và vào edit mode).");
                PlayerPrefs.SetInt(ENTER_SHOP_SETTING_EDIT_MODE_KEY, 1);
                PlayerPrefs.Save();

                StatusPopupManager.Instance.ShowPopup("Giấy phép sử dụng của bạn đã hết hạn. Vui lòng gia hạn để tiếp tục sử dụng ứng dụng.", () => {
                    SceneManager.LoadScene("ShopSetting");
                });
                return;
            }

            Debug.Log($"Người dùng {user.UserId} đã có tên shop và còn hạn. Chuyển đến Homepage.");
            PlayerPrefs.DeleteKey(ENTER_SHOP_SETTING_EDIT_MODE_KEY);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Homepage");
        });
    }

    // File: AuthManager.cs

    public void SignOutAndReturnToLogin()
    {
        if (auth != null && user != null)
        {
            Debug.Log("Đăng xuất và chuyển về màn hình Login.");

            // ************ HIỂN THỊ LOADING PANEL TOÀN CỤC ************
            if (fullScreenLoadingPrefab != null && _activeFullScreenLoadingPanel == null)
            {
                _activeFullScreenLoadingPanel = Instantiate(fullScreenLoadingPrefab);
                DontDestroyOnLoad(_activeFullScreenLoadingPanel);

                // Đặt text
                TMP_Text loadingText = _activeFullScreenLoadingPanel.GetComponentInChildren<TMP_Text>(true);
                if (loadingText != null)
                {
                    loadingText.text = "Đang đăng xuất...";
                }
            }
            else if (fullScreenLoadingPrefab == null)
            {
                Debug.LogError("AuthManager: fullScreenLoadingPrefab chưa được gán! Loading Panel sẽ không hiển thị.");
            }
            // ************************************************************

            // Đặt cờ ngay lập tức và xóa dữ liệu cục bộ
            AuthSession.ComingFromLogout = true;
            ShopSessionData.ClearAllData();
            user = null; // Cục bộ 'user' được reset

            // BỌC TOÀN BỘ QUÁ TRÌNH VÀO DISPATCHER ĐỂ XỬ LÝ LỖI RACE CONDITION
            UnityMainThreadDispatcher.Instance().Enqueue(() => {

                // Thực hiện lệnh đăng xuất trên Main Thread
                FirebaseAuth.DefaultInstance.SignOut();

                // THÊM: Đồng bộ biến auth cục bộ (theo yêu cầu của bạn)
                auth = FirebaseAuth.DefaultInstance;

                // Tải Scene Login
                UnityEngine.SceneManagement.SceneManager.LoadScene("Login");

                // Hủy Loading Panel sau khi scene mới được tải hoàn tất
                if (_activeFullScreenLoadingPanel != null)
                {
                     // Lệnh Destroy này sẽ được thực hiện sau khi tải scene Login
                     Destroy(_activeFullScreenLoadingPanel);
                     _activeFullScreenLoadingPanel = null;
                }
            });
        }
        else
        {
            Debug.Log("Không có người dùng nào đang đăng nhập để đăng xuất. Đảm bảo xóa cache.");
            ShopSessionData.ClearAllData();
            AuthSession.ComingFromLogout = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
        }
    }

    private bool IsValidEmail(string email)
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

    void OnDestroy()
    {
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
            auth = null;
        }
         UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}