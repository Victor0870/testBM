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
using static ShopSessionData;
using static ShopSettingManager;

public class SalesManager : MonoBehaviour
{
    [Header("Sales Sub-Managers")]
    public SalesCustomerManager customerManager;
    public SalesCartManager cartManager;
    public SalesFptInvoiceManager fptInvoiceManager;
    public SalesFinalizeTransaction finalizeTransactionManager;

    [Header("Sales Manager Main UI")]
    // ĐÃ XÓA: public Button addProductToCartMainButton; 
    public Button backToInventoryButton;

    [HideInInspector] public FirebaseFirestore db;
    [HideInInspector] public FirebaseUser currentUser;
    private ListenerRegistration productsListenerRegistration;

    private CollectionReference userProductsCollection;
    private CollectionReference userCustomersCollection;
    private CollectionReference userSalesCollection;

    private List<ProductData> allUserProducts = new List<ProductData>();

    public static SalesManager Instance { get; private set; }

    void Awake()
    {
        // SỬA LỖI SINGLETON: Hủy instance mới nếu đã có, nhưng cho phép tạo lại khi load scene
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        // KHÔNG DÙNG DontDestroyOnLoad Ở ĐÂY!

        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        if (customerManager == null) Debug.LogError("SalesManager: customerManager chưa được gán!");
        if (cartManager == null) Debug.LogError("SalesManager: cartManager chưa được gán!");
        if (fptInvoiceManager == null) Debug.LogError("SalesManager: fptInvoiceManager chưa được gán!");
        if (finalizeTransactionManager == null) Debug.LogError("SalesManager: finalizeTransactionManager chưa được gán!");
    }

    void Start()
    {
        // ĐÃ XÓA: addProductToCartMainButton.onClick...
        
        if (backToInventoryButton != null) backToInventoryButton.onClick.AddListener(OnBackToInventoryButtonClicked);

        InitializeSubManagers();
        CheckFeatureAccess();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null) auth.StateChanged -= AuthStateChanged;
        
        if (productsListenerRegistration != null) productsListenerRegistration.Dispose();

        if (cartManager != null && finalizeTransactionManager != null)
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
            if (signedIn)
            {
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                userCustomersCollection = db.Collection("shops").Document(currentUser.UserId).Collection("customers");
                userSalesCollection = db.Collection("shops").Document(currentUser.UserId).Collection("sales");
                
                SetupProductsListener();
                InitializeSubManagers();
            }
            CheckFeatureAccess();
        }
    }

    private void InitializeSubManagers()
    {
        if (StatusPopupManager.Instance == null) return;

        if (customerManager != null)
            customerManager.Initialize(db, currentUser, userCustomersCollection, StatusPopupManager.Instance);

        if (cartManager != null)
        {
            cartManager.Initialize(db, currentUser, userProductsCollection, allUserProducts, StatusPopupManager.Instance);
            cartManager.OnCartChanged -= finalizeTransactionManager.UpdateCartSummaryUI;
            cartManager.OnCartChanged += finalizeTransactionManager.UpdateCartSummaryUI;
        }

        if (fptInvoiceManager != null)
            fptInvoiceManager.Initialize(db, currentUser, userSalesCollection, ShopSessionData.CachedShopSettings);

        if (finalizeTransactionManager != null)
        {
             finalizeTransactionManager.Initialize(db, currentUser, userSalesCollection, userProductsCollection,
                                                  customerManager, cartManager, fptInvoiceManager, StatusPopupManager.Instance,
                                                  customerManager.customerLookupStatusText);
        }
    }

    private void SetupProductsListener()
    {
        if (userProductsCollection == null) return;
        if (productsListenerRegistration != null) productsListenerRegistration.Dispose();

       productsListenerRegistration = userProductsCollection.Listen(snapshot =>
       {
           UnityMainThreadDispatcher.Instance().Enqueue(() =>
           {
               allUserProducts.Clear();
               foreach (DocumentSnapshot doc in snapshot.Documents)
               {
                   if (doc.Exists)
                   {
                       ProductData product = doc.ConvertTo<ProductData>();
                       product.productId = doc.Id;
                       allUserProducts.Add(product);
                   }
               }
               if (cartManager != null) cartManager.SetAllUserProducts(allUserProducts);
           });
       });
    }

    private void CheckFeatureAccess()
    {
        string currentPackageName = ShopSessionData.CachedShopSettings?.packageType;
        if (AuthManager.GlobalAppConfig == null || ShopSessionData.AppPackageConfig == null) return;

        bool hasSalesFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.Sales);
        bool hasEInvoiceFeature = ShopSessionData.AppPackageConfig.HasFeature(currentPackageName, AppFeature.EInvoice);

        SetSalesFeaturesInteractable(hasSalesFeature);

        if (fptInvoiceManager != null && fptInvoiceManager.exportInvoiceButton != null)
        {
            fptInvoiceManager.exportInvoiceButton.interactable = hasEInvoiceFeature;
        }
    }

    private void SetSalesFeaturesInteractable(bool interactable)
    {
        // ĐÃ XÓA: addProductToCartMainButton.interactable = interactable;
        
        if (customerManager != null) customerManager.SetAllCustomerInputFieldsInteractable(interactable);

        if (cartManager != null)
        {
            if (cartManager.productSearchInputField != null) cartManager.productSearchInputField.interactable = interactable;
            if (cartManager.scanBarcodeButton != null) cartManager.scanBarcodeButton.interactable = interactable;
            if (cartManager.closeProductSelectionPopupButton != null) cartManager.closeProductSelectionPopupButton.interactable = interactable;
            
            if (cartManager.cartAndAddProductAreaRect != null)
            {
                CanvasGroup canvasGroup = cartManager.cartAndAddProductAreaRect.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = cartManager.cartAndAddProductAreaRect.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
                canvasGroup.alpha = interactable ? 1f : 0.5f;
            }
        }

        if (finalizeTransactionManager != null)
        {
            if (finalizeTransactionManager.completeSaleButton != null) finalizeTransactionManager.completeSaleButton.interactable = interactable;
            if (finalizeTransactionManager.cancelSaleButton != null) finalizeTransactionManager.cancelSaleButton.interactable = interactable;
        }
    }

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
