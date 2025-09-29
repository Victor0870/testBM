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
using daBizmate; // Cần thiết để truy cập các entity BGDatabase (E_Customer, E_Sale)
using static ShopSessionData; // Truy cập AppPackageConfig

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

    // --- Events ---
    // Event để thông báo khi danh sách Đơn hàng (SaleData) thay đổi/tải xong
    public event Action<List<SaleData>> OnSalesLoaded;
    // Event để thông báo khi danh sách Khách hàng (CustomerData) thay đổi/tải xong
    public event Action<List<CustomerData>> OnCustomersLoaded;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
        // Thêm logic Dispose Listener Firestore tại đây
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
        {
            Debug.Log("SalesDataService: Đang hoạt động ở chế độ CLOUD SYNC (Firebase).");
        }
        else
        {
            Debug.LogWarning("SalesDataService: Đang hoạt động ở chế độ LOCAL ONLY (BGDatabase).");
        }
    }

    // --- Di chuyển Dữ liệu Local lên Cloud (Migration Logic) ---

    public async Task MigrateLocalDataToCloud()
    {
        if (!IsCloudSyncEnabled || _isMigrating)
        {
            Debug.LogWarning("Migration bị hủy: Cloud Sync không được bật hoặc quá trình di chuyển đang diễn ra.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Không thể di chuyển dữ liệu Cloud.");
            return;
        }


        _isMigrating = true;
        StatusPopupManager.Instance.ShowPopup("Đang tiến hành đồng bộ dữ liệu cục bộ lên Cloud. Vui lòng chờ...");

        try
        {
            // 1. Di chuyển Khách hàng
            await MigrateChunked<CustomerData>(
                LoadUnsyncedCustomersFromLocalDB(),
                SaveCustomerToFirestoreBatch
            );

            // 2. Di chuyển Đơn hàng (Sales)
            await MigrateChunked<SaleData>(
                LoadUnsyncedSalesFromLocalDB(),
                SaveSaleToFirestoreBatch
            );

            // 3. TODO: Di chuyển Sản phẩm (Inventory)

            StatusPopupManager.Instance.ShowPopup("Di chuyển dữ liệu thành công! Ứng dụng đã sẵn sàng cho đồng bộ Cloud.");
            // Cần gọi lại LoadAndListenForData() sau khi Migration hoàn tất
        }
        catch (Exception e)
        {
            StatusPopupManager.Instance.ShowPopup($"Lỗi di chuyển dữ liệu: {e.GetBaseException().Message}. Vui lòng thử lại.");
            Debug.LogError($"Lỗi Migration: {e.Message}");
        }
        finally
        {
            _isMigrating = false;
        }
    }

    // Hàm chung xử lý di chuyển theo khối (Chunked Migration Logic)
    private async Task MigrateChunked<T>(
        List<T> localData,
        Func<List<T>, Task<List<(string oldLocalId, string newCloudId)>>> saveBatchFunc) where T : class
    {
        if (localData == null || localData.Count == 0) return;

        int totalCount = localData.Count;
        int processedCount = 0;

        while (processedCount < totalCount)
        {
            // Lấy khối dữ liệu
            List<T> chunk = localData.Skip(processedCount).Take(FIREBASE_BATCH_SIZE).ToList();

            // Gửi Batch lên Firebase
            List<(string oldLocalId, string newCloudId)> batchResults = await saveBatchFunc(chunk);

            // Cập nhật Local DB với ID Cloud mới
            foreach (var result in batchResults)
            {
                // TODO: Ánh xạ lại ID Cloud vào BGDatabase Entity
            }

            processedCount += chunk.Count;
            StatusPopupManager.Instance.ShowPopup($"Đang di chuyển: {processedCount}/{totalCount} bản ghi...");
        }
    }

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

    public async Task SaveCustomerDataLocally(CustomerData customer)
    {
        // TODO: Logic tìm và cập nhật/thêm entity E_Customer trong BGDatabase
        await Task.CompletedTask;
    }

    // Phương thức được gọi sau khi FPT Invoice được tạo thành công để cập nhật lại SaleData cục bộ
    public async Task UpdateSaleDataLocallyWithInvoiceInfo(SaleData sale)
    {
        // TODO: Logic tìm entity E_Sale theo sale.saleId và cập nhật các trường HĐĐT (fptInvoiceId, fptLookupLink...)
        await Task.CompletedTask;
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

    public async Task SaveSaleDataLocally(SaleData sale)
    {
        // TODO: Logic tìm và cập nhật/thêm entity E_Sale và E_SaleItem trong BGDatabase
        await Task.CompletedTask;
    }

    // --- Logic Xóa (Delete) ---

    public async Task DeleteCustomerDataAsync(string customerId)
    {
        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
            await db.Collection(userCustomersPath).Document(customerId).DeleteAsync();
        }
        // TODO: Logic xóa cục bộ (BGDatabase)
        await Task.CompletedTask;
    }

    // --- Logic Tải Dữ liệu (Load) ---

    public void LoadAndListenForCustomers()
    {
        if (IsCloudSyncEnabled)
        {
            // TODO: Thiết lập Listener Firestore và gọi OnCustomersLoaded?.Invoke(customers);
        }
        else
        {
            // TODO: Tải từ Local DB và gọi OnCustomersLoaded?.Invoke(customers);
        }
    }

    public void LoadAndListenForSales()
    {
        if (IsCloudSyncEnabled)
        {
            // TODO: Thiết lập Listener Firestore và gọi OnSalesLoaded?.Invoke(sales);
        }
        else
        {
            // TODO: Tải từ Local DB và gọi OnSalesLoaded?.Invoke(sales);
        }
    }

    // Phương thức cần thiết cho SaleOrderDetailPanel để lấy thông tin chi tiết khách hàng theo ID
    public async Task<CustomerData> GetCustomerDataBySaleIdAsync(string customerId)
    {
        // TODO: Logic tìm CustomerData từ Cloud (nếu CloudSync) hoặc Local (nếu LocalOnly)
        return new CustomerData(); // Placeholder
    }

    // Phương thức cần thiết cho SaleOrderDetailPanel để ánh xạ SaleItem sang ProductData Snapshot
    public Dictionary<string, ProductData> ConvertSaleItemsToProductSnapshot(List<SaleItem> items)
    {
        // TODO: Logic chuyển đổi SaleItem sang ProductData để sử dụng trong FPT Manager
        return new Dictionary<string, ProductData>();
    }

    // --- Phương thức Ánh xạ (Mapping Helpers) ---

    // ĐÃ XÓA CÁC HÀM GÂY LỖI CS0111
    private List<CustomerData> LoadUnsyncedCustomersFromLocalDB()
    {
        // TODO: Logic tìm kiếm các E_Customer có f_customerId rỗng
        return new List<CustomerData>();
    }

    private List<SaleData> LoadUnsyncedSalesFromLocalDB()
    {
        // TODO: Logic tìm kiếm các E_Sale có f_saleId rỗng
        return new List<SaleData>();
    }


    // --- Phương thức Giao tiếp Firebase (Cloud) ---

    private async Task<string> SaveCustomerToFirestore(CustomerData customer)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
        CollectionReference customersRef = db.Collection(userCustomersPath);
        DocumentReference docRef;

        if (string.IsNullOrEmpty(customer.customerId))
        {
            docRef = await customersRef.AddAsync(customer);
        }
        else
        {
            docRef = customersRef.Document(customer.customerId);
            await docRef.SetAsync(customer, SetOptions.MergeAll);
        }
        return docRef.Id;
    }

    private async Task<string> SaveSaleToFirestore(SaleData sale)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
        CollectionReference salesRef = db.Collection(userSalesPath);
        DocumentReference docRef;

        if (string.IsNullOrEmpty(sale.saleId))
        {
            docRef = await salesRef.AddAsync(sale);
        }
        else
        {
            docRef = salesRef.Document(sale.saleId);
            await docRef.SetAsync(sale, SetOptions.MergeAll);
        }
        return docRef.Id;
    }

    // Phương thức lưu hàng loạt cho Migration
    private async Task<List<(string oldLocalId, string newCloudId)>> SaveCustomerToFirestoreBatch(List<CustomerData> chunk)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
        WriteBatch batch = db.StartBatch();
        CollectionReference customersRef = db.Collection(userCustomersPath);

        List<(string oldLocalId, string newCloudId)> results = new List<(string oldLocalId, string newCloudId)>();

        foreach (var customer in chunk)
        {
            DocumentReference newDocRef = customersRef.Document();
            batch.Set(newDocRef, customer);
            results.Add((customer.customerId, newDocRef.Id));
        }

        await batch.CommitAsync();
        return results;
    }

    // Phương thức lưu hàng loạt cho Migration
    private async Task<List<(string oldLocalId, string newCloudId)>> SaveSaleToFirestoreBatch(List<SaleData> chunk)
    {
        if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
        WriteBatch batch = db.StartBatch();
        CollectionReference salesRef = db.Collection(userSalesPath);

        List<(string oldLocalId, string newCloudId)> results = new List<(string oldLocalId, string newCloudId)>();

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