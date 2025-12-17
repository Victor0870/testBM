// File: Scripts/DataServices/SalesDataService.cs
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using BansheeGz.BGDatabase;
// using daBizmate; // Bỏ comment nếu namespace BGDatabase của bạn là daBizmate
using static ShopSessionData;

public class SalesDataService : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static SalesDataService Instance { get; private set; }

    // --- Firebase References ---
    private FirebaseFirestore db;
    private FirebaseUser currentUser;
    private string userCustomersPath;
    private string userSalesPath;

    private const int FIREBASE_BATCH_SIZE = 500; // Giới hạn Firebase Write Batch

    // --- Trạng thái ---
    public bool IsCloudSyncEnabled { get; private set; } = false;
    private bool _isMigrating = false;

    // --- Quản lý Listener (FIX LỖI MEMORY LEAK) ---
    private ListenerRegistration _salesListenerRegistration;

    // --- Events ---
    public event Action<List<SaleData>> OnSalesLoaded;
    public event Action<List<CustomerData>> OnCustomersLoaded;

    // --- Cache Dữ liệu ---
    private List<SaleData> _cachedSales = new List<SaleData>();

    void Awake()
    {
        // Singleton chuẩn: Hủy object mới nếu đã có Instance cũ
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeFirebase();
    }

    private void OnDestroy()
    {
        FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
        
        // Hủy listener khi Service bị hủy
        if (_salesListenerRegistration != null)
        {
            _salesListenerRegistration.Stop();
            _salesListenerRegistration = null;
        }
    }

    void InitializeFirebase()
    {
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth.DefaultInstance.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        FirebaseUser newUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            
            // Nếu đổi user, hủy listener cũ ngay lập tức
            if (_salesListenerRegistration != null)
            {
                _salesListenerRegistration.Stop();
                _salesListenerRegistration = null;
                _cachedSales.Clear();
            }

            if (currentUser != null)
            {
                string userId = currentUser.UserId;
                userCustomersPath = $"shops/{userId}/customers";
                userSalesPath = $"shops/{userId}/sales";
            }
            else
            {
                userCustomersPath = null;
                userSalesPath = null;
            }
            CheckCloudSyncStatus();
        }
    }

    // --- Kiểm soát Chế độ hoạt động ---
    public void CheckCloudSyncStatus()
    {
        string currentPackageName = CachedShopSettings?.packageType;
        IsCloudSyncEnabled = AppPackageConfig != null &&
                             AppPackageConfig.HasFeature(currentPackageName, AppFeature.CloudSync);

        if (IsCloudSyncEnabled)
            Debug.Log("SalesDataService: Đang hoạt động ở chế độ CLOUD SYNC (Firebase).");
        else
            Debug.LogWarning("SalesDataService: Đang hoạt động ở chế độ LOCAL ONLY (BGDatabase).");
    }

    // =================================================================================
    // --- PHẦN LOGIC TẢI DỮ LIỆU (QUAN TRỌNG NHẤT - ĐÃ SỬA) ---
    // =================================================================================
    
    public void LoadAndListenForSales()
    {
        if (IsCloudSyncEnabled)
        {
            // --- LOGIC CLOUD (Firestore Realtime) ---
            if (currentUser == null || string.IsNullOrEmpty(userSalesPath)) return;

            // FIX: Nếu đã có listener đang chạy thì không tạo thêm
            if (_salesListenerRegistration != null)
            {
                if (_cachedSales.Count > 0) OnSalesLoaded?.Invoke(_cachedSales);
                return;
            }

            Debug.Log("SalesDataService: Bắt đầu lắng nghe đơn hàng từ Cloud...");
            CollectionReference salesRef = db.Collection(userSalesPath);

            _salesListenerRegistration = salesRef.Listen(snapshot =>
            {
                List<SaleData> sales = new List<SaleData>();
                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        try
                        {
                            SaleData s = doc.ConvertTo<SaleData>();
                            s.saleId = doc.Id;
                            sales.Add(s);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Lỗi parse đơn hàng {doc.Id}: {ex.Message}");
                        }
                    }
                }

                _cachedSales = sales;

                // Đẩy về Main Thread để cập nhật UI Homepage/Report
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnSalesLoaded?.Invoke(_cachedSales);
                });
                
                Debug.Log($"SalesDataService: Đã tải/cập nhật {_cachedSales.Count} đơn hàng.");
            });
        }
        else
        {
            // --- LOGIC LOCAL (BGDatabase) ---
            List<SaleData> localSales = new List<SaleData>();

            // TODO: Bỏ comment đoạn này khi bạn đã setup xong BGDatabase Entity
            /*
            if (daBizmate.E_Sale.MetaDefault != null)
            {
                daBizmate.E_Sale.ForEachEntity(entity =>
                {
                    SaleData s = new SaleData();
                    s.saleId = entity.f_saleId;
                    s.totalAmount = (long)entity.f_totalAmount;
                    // Chuyển đổi DateTime sang Timestamp để đồng bộ format
                    s.saleDate = Timestamp.FromDateTime(entity.f_saleDate); 
                    
                    // Mapping các trường khác...
                    localSales.Add(s);
                });
            }
            */
            
            // Tạm thời log để biết đang chạy Local
            Debug.Log("SalesDataService: Load Local Data (Placeholder).");

            _cachedSales = localSales;
            OnSalesLoaded?.Invoke(_cachedSales);
        }
    }

    public void LoadAndListenForCustomers()
    {
        // TODO: Logic tương tự như Sales nếu cần hiển thị danh sách khách hàng realtime
        // Hiện tại để trống hoặc gọi Load 1 lần nếu cần.
    }

    // =================================================================================
    // --- PHẦN LOGIC CRUD CŨ CỦA BẠN (GIỮ NGUYÊN) ---
    // =================================================================================

    // --- Logic Lưu trữ Khách hàng (CRUD/Save Customer) ---
    public async Task<string> SaveCustomerDataAsync(CustomerData customer)
    {
        if (IsCloudSyncEnabled)
        {
            // 1. Lưu/Cập nhật lên Cloud
            string cloudId = await SaveCustomerToFirestore(customer);
            customer.customerId = cloudId;

            // 2. Lưu xuống Local (đánh dấu đã sync)
            await SaveCustomerDataLocally(customer);
            return cloudId;
        }
        else
        {
            // 1. Chỉ lưu vào Local DB
            if (string.IsNullOrEmpty(customer.customerId)) customer.customerId = Guid.NewGuid().ToString();
            await SaveCustomerDataLocally(customer);
            return customer.customerId;
        }
    }

    // --- Logic Lưu trữ Đơn hàng (CRUD/Save Sale) ---
    public async Task<string> SaveSaleDataAsync(SaleData sale)
    {
        if (IsCloudSyncEnabled)
        {
            // 1. Lưu/Cập nhật lên Cloud
            string cloudId = await SaveSaleToFirestore(sale);
            sale.saleId = cloudId;

            // 2. Lưu xuống Local (đánh dấu đã sync)
            await SaveSaleDataLocally(sale);
            return cloudId;
        }
        else
        {
            // 1. Chỉ lưu vào Local DB
            if (string.IsNullOrEmpty(sale.saleId)) sale.saleId = Guid.NewGuid().ToString();
            await SaveSaleDataLocally(sale);
            return sale.saleId;
        }
    }

    // --- Logic Di chuyển Dữ liệu Local lên Cloud (Migration) ---
    public async Task MigrateLocalDataToCloud()
    {
        if (!IsCloudSyncEnabled || _isMigrating)
        {
            Debug.LogWarning("Migration bị hủy: Cloud Sync không được bật hoặc quá trình đang diễn ra.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Không thể di chuyển dữ liệu Cloud.");
            return;
        }

        _isMigrating = true;
        StatusPopupManager.Instance.ShowPopup("Đang tiến hành đồng bộ dữ liệu cục bộ lên Cloud...");

        try
        {
            // 1. Di chuyển Khách hàng
            await MigrateChunked<CustomerData>(
                LoadUnsyncedCustomersFromLocalDB(),
                SaveCustomerToFirestoreBatch
            );

            // 2. Di chuyển Đơn hàng
            await MigrateChunked<SaleData>(
                LoadUnsyncedSalesFromLocalDB(),
                SaveSaleToFirestoreBatch
            );

            StatusPopupManager.Instance.ShowPopup("Di chuyển dữ liệu thành công!");
            // Gọi lại load data để refresh listener
            LoadAndListenForSales();
        }
        catch (Exception e)
        {
            StatusPopupManager.Instance.ShowPopup($"Lỗi di chuyển dữ liệu: {e.GetBaseException().Message}");
            Debug.LogError($"Lỗi Migration: {e.Message}");
        }
        finally
        {
            _isMigrating = false;
        }
    }

    private async Task MigrateChunked<T>(
        List<T> localData,
        Func<List<T>, Task<List<(string oldLocalId, string newCloudId)>>> saveBatchFunc) where T : class
    {
        if (localData == null || localData.Count == 0) return;

        int totalCount = localData.Count;
        int processedCount = 0;

        while (processedCount < totalCount)
        {
            List<T> chunk = localData.Skip(processedCount).Take(FIREBASE_BATCH_SIZE).ToList();
            await saveBatchFunc(chunk);
            // TODO: Cập nhật lại ID Local sau khi batch thành công
            processedCount += chunk.Count;
            StatusPopupManager.Instance.ShowPopup($"Đang di chuyển: {processedCount}/{totalCount} bản ghi...");
        }
    }

    // --- Logic Xóa (Delete) ---
    public async Task DeleteCustomerDataAsync(string customerId)
    {
        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in.");
            await db.Collection(userCustomersPath).Document(customerId).DeleteAsync();
        }
        // TODO: Logic xóa cục bộ (BGDatabase)
        await Task.CompletedTask;
    }

    // --- Helpers cho SaleOrderDetailPanel ---
    public async Task<CustomerData> GetCustomerDataBySaleIdAsync(string customerId)
    {
        // Placeholder: Cần implement logic tìm customer
        return await Task.FromResult(new CustomerData()); 
    }

    public Dictionary<string, ProductData> ConvertSaleItemsToProductSnapshot(List<SaleItem> items)
    {
        // Placeholder: Logic chuyển đổi SaleItem sang ProductData
        return new Dictionary<string, ProductData>();
    }

    // --- Local DB Helpers (Placeholder - Cần bạn implement BGDatabase) ---
    public async Task SaveCustomerDataLocally(CustomerData customer)
    {
        // TODO: Implement BGDatabase Save logic
        await Task.CompletedTask;
    }

    public async Task SaveSaleDataLocally(SaleData sale)
    {
        // TODO: Implement BGDatabase Save logic
        await Task.CompletedTask;
    }

    public async Task UpdateSaleDataLocallyWithInvoiceInfo(SaleData sale)
    {
        // TODO: Implement BGDatabase Update logic
        await Task.CompletedTask;
    }

    private List<CustomerData> LoadUnsyncedCustomersFromLocalDB()
    {
        // TODO: Implement logic tìm Customer chưa sync
        return new List<CustomerData>();
    }

    private List<SaleData> LoadUnsyncedSalesFromLocalDB()
    {
        // TODO: Implement logic tìm Sale chưa sync
        return new List<SaleData>();
    }

    // --- Firebase Cloud Helpers ---
    private async Task<string> SaveCustomerToFirestore(CustomerData customer)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in.");
        CollectionReference customersRef = db.Collection(userCustomersPath);
        DocumentReference docRef;

        if (string.IsNullOrEmpty(customer.customerId))
            docRef = await customersRef.AddAsync(customer);
        else
        {
            docRef = customersRef.Document(customer.customerId);
            await docRef.SetAsync(customer, SetOptions.MergeAll);
        }
        return docRef.Id;
    }

    private async Task<string> SaveSaleToFirestore(SaleData sale)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in.");
        CollectionReference salesRef = db.Collection(userSalesPath);
        DocumentReference docRef;

        if (string.IsNullOrEmpty(sale.saleId))
            docRef = await salesRef.AddAsync(sale);
        else
        {
            docRef = salesRef.Document(sale.saleId);
            await docRef.SetAsync(sale, SetOptions.MergeAll);
        }
        return docRef.Id;
    }

    private async Task<List<(string oldLocalId, string newCloudId)>> SaveCustomerToFirestoreBatch(List<CustomerData> chunk)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in.");
        WriteBatch batch = db.StartBatch();
        CollectionReference customersRef = db.Collection(userCustomersPath);
        List<(string oldLocalId, string newCloudId)> results = new List<(string, string)>();

        foreach (var customer in chunk)
        {
            DocumentReference newDocRef = customersRef.Document();
            batch.Set(newDocRef, customer);
            results.Add((customer.customerId, newDocRef.Id));
        }
        await batch.CommitAsync();
        return results;
    }

    private async Task<List<(string oldLocalId, string newCloudId)>> SaveSaleToFirestoreBatch(List<SaleData> chunk)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in.");
        WriteBatch batch = db.StartBatch();
        CollectionReference salesRef = db.Collection(userSalesPath);
        List<(string oldLocalId, string newCloudId)> results = new List<(string, string)>();

        foreach (var sale in chunk)
        {
            DocumentReference newDocRef = salesRef.Document();
            batch.Set(newDocRef, sale);
            results.Add((sale.saleId, newDocRef.Id));
        }
        await batch.CommitAsync();
        return results;
    }
}
