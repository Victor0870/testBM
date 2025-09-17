using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Linq;

// Using cho các Manager con mới
// Không cần SimpleJSON ở đây nữa
using static ShopSessionData;
using static ShopSettingManager;

public class SalesManager : MonoBehaviour
{
    // --- KHAI BÁO CÁC MANAGER CON ---
    [Header("Sales Sub-Managers")]
    public SalesCustomerManager customerManager;
    public SalesCartManager cartManager;
    public SalesFptInvoiceManager fptInvoiceManager;
    public SalesFinalizeTransaction finalizeTransactionManager;

    // --- UI CỦA SALES MANAGER CHÍNH (những UI không thuộc về manager con nào) ---
    [Header("Sales Manager Main UI")]
    public Button addProductToCartMainButton; // Nút "Thêm sản phẩm" trên UI giỏ hàng (sẽ gọi SalesCartManager)
    public Button backToInventoryButton; // Nút điều hướng
    //public Button completeSaleButton;
    //public Button cancelButton;
    // --- Firebase Core ---

    [HideInInspector] public FirebaseFirestore db;
    [HideInInspector] public FirebaseUser currentUser;
    private ListenerRegistration productsListenerRegistration;

    // --- Firebase Collections (sẽ được truyền cho các Manager con) ---
    private CollectionReference userProductsCollection;
    private CollectionReference userCustomersCollection;
    private CollectionReference userSalesCollection;

    // --- Danh sách sản phẩm chung (được SalesCartManager sử dụng) ---
    private List<ProductData> allUserProducts = new List<ProductData>(); // Được SalesCartManager sử dụng

    public static SalesManager Instance { get; private set; } // Thêm dòng này
    void Awake()
    {
        if (Instance == null)
        {
           Instance = this;
           // DontDestroyOnLoad(this.gameObject); // Không dùng DontDestroyOnLoad cho Scene Manager
        }
         else
        {
           Destroy(this.gameObject);
        }
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        // Đảm bảo các manager con được gán trong Inspector
        if (customerManager == null) Debug.LogError("SalesManager: customerManager chưa được gán!");
        if (cartManager == null) Debug.LogError("SalesManager: cartManager chưa được gán!");
        if (fptInvoiceManager == null) Debug.LogError("SalesManager: fptInvoiceManager chưa được gán!");
        if (finalizeTransactionManager == null) Debug.LogError("SalesManager: finalizeTransactionManager chưa được gán!");
    }

    void Start()
    {
        // Gán listener cho nút "Thêm sản phẩm" chính (nếu nó không được tự tạo trong CartManager)
        if (addProductToCartMainButton != null)
        {
            addProductToCartMainButton.onClick.AddListener(OnAddProductToCartMainButtonClicked);
        }
        else
        {
            Debug.LogError("SalesManager: addProductToCartMainButton chưa được gán!");
        }

        if (backToInventoryButton != null) backToInventoryButton.onClick.AddListener(OnBackToInventoryButtonClicked);

        // Khởi tạo các Manager con và truyền các dependency cần thiết
        InitializeSubManagers();

        // Kiểm tra quyền truy cập tính năng khi Scene khởi động
        CheckFeatureAccess();
    }

    void OnDestroy()
    {
        FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
        if (productsListenerRegistration != null)
        {
            productsListenerRegistration.Dispose();
        }

        // Hủy đăng ký sự kiện từ CartManager để tránh rò rỉ bộ nhớ
        if (cartManager != null)
        {
            cartManager.OnCartChanged -= finalizeTransactionManager.UpdateCartSummaryUI;
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != currentUser)
        {
            currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            bool signedIn = currentUser != null;
            if (!signedIn && currentUser == null)
            {
                Debug.Log("Người dùng đã đăng xuất.");
                // Xử lý khi người dùng đăng xuất (ví dụ: quay lại màn hình đăng nhập)
            }
            if (signedIn)
            {
                Debug.Log($"Người dùng đã đăng nhập: {currentUser.DisplayName ?? currentUser.Email}");
                // Khởi tạo lại các collection và listener khi user đăng nhập
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                userCustomersCollection = db.Collection("shops").Document(currentUser.UserId).Collection("customers");
                userSalesCollection = db.Collection("shops").Document(currentUser.UserId).Collection("sales");
                Debug.Log($"SalesManager: User collections initialized for UID: {currentUser.UserId}");
                SetupProductsListener();

                // Cập nhật các manager con với user và collections mới
                InitializeSubManagers();
            }
            CheckFeatureAccess();
        }
    }

    private void InitializeSubManagers()
    {
        // Đảm bảo StatusPopupManager.Instance đã có
        if (StatusPopupManager.Instance == null)
        {
            Debug.LogError("SalesManager: StatusPopupManager.Instance is null. Please ensure it is initialized early in the app lifecycle.");
            return;
        }
        // Đảm bảo FptEInvoiceApiClient.Instance đã có
        if (FptEInvoiceApiClient.Instance == null)
        {
            Debug.LogError("SalesManager: FptEInvoiceApiClient.Instance is null. Please ensure it is initialized early in the app lifecycle.");
        }


        // Truyền các dependency cho SalesCustomerManager
        if (customerManager != null)
        {
            customerManager.Initialize(db, currentUser, userCustomersCollection, StatusPopupManager.Instance);
        }

        // Truyền các dependency cho SalesCartManager
        if (cartManager != null)
        {
            // SalesCartManager sẽ cần allUserProducts
            cartManager.Initialize(db, currentUser, userProductsCollection, allUserProducts, StatusPopupManager.Instance);
            // Đăng ký lắng nghe sự kiện từ CartManager để cập nhật UI tổng tiền
            cartManager.OnCartChanged -= finalizeTransactionManager.UpdateCartSummaryUI; // Gỡ bỏ cũ để tránh trùng lặp
            cartManager.OnCartChanged += finalizeTransactionManager.UpdateCartSummaryUI;
        }

        // Truyền các dependency cho SalesFptInvoiceManager
        if (fptInvoiceManager != null)
        {
            fptInvoiceManager.Initialize(db, currentUser, userSalesCollection, FptEInvoiceApiClient.Instance, StatusPopupManager.Instance);
        }

        // Truyền các dependency cho SalesFinalizeTransaction
        if (finalizeTransactionManager != null)
        {
            finalizeTransactionManager.Initialize(db, currentUser, userSalesCollection, userProductsCollection,
                                                  customerManager, cartManager, fptInvoiceManager, StatusPopupManager.Instance,
                                                  customerManager.customerLookupStatusText); // Truyền tham chiếu Text
        }
    }


    private void SetupProductsListener()
    {
        if (userProductsCollection == null)
        {
            Debug.LogError("SetupProductsListener: userProductsCollection is null. Cannot set up listener.");
            return;
        }

        if (productsListenerRegistration != null)
        {
            productsListenerRegistration.Dispose();
        }

       productsListenerRegistration = userProductsCollection.Listen(snapshot =>
       {
           UnityMainThreadDispatcher.Instance().Enqueue(() =>
           {
               Debug.Log("SalesManager: Đang đồng bộ toàn bộ danh sách sản phẩm...");

               allUserProducts.Clear(); // Cập nhật danh sách chính
               foreach (DocumentSnapshot doc in snapshot.Documents)
               {
                   if (doc.Exists)
                   {
                       ProductData product = doc.ConvertTo<ProductData>();
                       product.productId = doc.Id;
                       allUserProducts.Add(product);
                   }
               }

               Debug.Log($"SalesManager: allUserProducts hiện có {allUserProducts.Count} sản phẩm.");
               // Báo cho CartManager biết danh sách sản phẩm đã được cập nhật
               if (cartManager != null)
               {
                   cartManager.SetAllUserProducts(allUserProducts);
               }
           });
       });
    }

    // THÊM PHƯƠNG THỨC KIỂM TRA QUYỀN TRUY CẬP TÍNH NĂNG
    private void CheckFeatureAccess()
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;

        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null)
        {
            Debug.LogWarning("SalesManager: Cấu hình gói hoặc App Package Config chưa được tải. Vô hiệu hóa các tính năng bán hàng/hóa đơn.");
            SetSalesFeaturesInteractable(false, "Không thể tải cấu hình gói ứng dụng.");
            return;
        }

        bool hasSalesFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales);
        bool hasEInvoiceFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.EInvoice);

        if (!hasSalesFeature)
        {
            SetSalesFeaturesInteractable(false, $"Gói hiện tại: '{currentPackageName}' không có quyền truy cập tính năng Bán hàng. Vui lòng liên hệ quản trị.");
            Debug.LogWarning($"SalesManager: Gói '{currentPackageName}' không có quyền truy cập tính năng Bán hàng.");
            return;
        }

        SetSalesFeaturesInteractable(true); // Mặc định kích hoạt nếu có salesFeature

        // Kiểm soát nút xuất hóa đơn FPT dựa trên tính năng EInvoice
        if (fptInvoiceManager != null && fptInvoiceManager.exportInvoiceButton != null)
        {
            fptInvoiceManager.exportInvoiceButton.interactable = hasEInvoiceFeature;
            if (!hasEInvoiceFeature)
            {
                Debug.Log($"SalesManager: Nút Xuất hóa đơn bị vô hiệu hóa. Gói '{currentPackageName}' không có tính năng Hóa đơn điện tử.");
            }
        }
    }

    // THÊM PHƯƠNG THỨC ĐỂ ĐIỀU KHIỂN SỰ TƯƠNG TÁC CỦA CÁC PHẦN TỬ UI LIÊN QUAN ĐẾN BÁN HÀNG
    private void SetSalesFeaturesInteractable(bool interactable, string message = null)
    {
        // Điều khiển nút "Thêm sản phẩm" chính
        if (addProductToCartMainButton != null) addProductToCartMainButton.interactable = interactable;

        // Các nút và InputField trong các Manager con sẽ được SetInteractable của chúng điều khiển.
        // SalesManager chỉ điều khiển các phần UI thuộc về nó.
        // Hoặc có thể gọi SetInteractable của các Manager con nếu chúng có public method.
        if (customerManager != null) customerManager.SetAllCustomerInputFieldsInteractable(interactable);
        // Lưu ý: customerPhoneInputField.interactable vẫn luôn được giữ true trong CustomerManager

        // Các UI thuộc CartManager
        if (cartManager != null)
        {
            // Điều khiển các nút trong giỏ hàng (nút tăng giảm, xóa)
            // cartManager.SetCartItemsInteractable(interactable); // Nếu có hàm này trong CartManager
            // Điều khiển các input/buttons trong popup tìm kiếm sản phẩm
            if (cartManager.productSearchInputField != null) cartManager.productSearchInputField.interactable = interactable;
            if (cartManager.scanBarcodeButton != null) cartManager.scanBarcodeButton.interactable = interactable;
            if (cartManager.closeProductSelectionPopupButton != null) cartManager.closeProductSelectionPopupButton.interactable = interactable;

            // Vô hiệu hóa toàn bộ khu vực giỏ hàng (Cart & Add Product Area)
            if (cartManager.cartAndAddProductAreaRect != null)
            {
                CanvasGroup canvasGroup = cartManager.cartAndAddProductAreaRect.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = cartManager.cartAndAddProductAreaRect.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
                canvasGroup.alpha = interactable ? 1f : 0.5f;
            }
        }

        // Các UI thuộc FinalizeTransactionManager
        if (finalizeTransactionManager != null)
        {
            if (finalizeTransactionManager.completeSaleButton != null) finalizeTransactionManager.completeSaleButton.interactable = interactable;
            if (finalizeTransactionManager.cancelSaleButton != null) finalizeTransactionManager.cancelSaleButton.interactable = interactable;
        }

        if (!interactable && !string.IsNullOrEmpty(message))
        {
            // StatusPopupManager.Instance.ShowPopup(message);
        }
    }


    // --- CÁC PHƯƠNG THỨC GỌI ĐIỀU PHỐI ĐẾN MANAGER CON ---
    public void OnAddProductToCartMainButtonClicked()
    {
        cartManager?.OnAddProductToCartMainButtonClicked();
    }


    public void OnExportInvoiceButtonClicked()
    {
        fptInvoiceManager?.OnExportInvoiceButtonClicked();
    }

    public void OnBackToInventoryButtonClicked()
    {
        SceneManager.LoadScene("Inventory");
    }
}