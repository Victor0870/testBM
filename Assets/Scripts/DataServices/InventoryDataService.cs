// File: InventoryDataService.cs
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using BansheeGz.BGDatabase;
using daBizmate;
using UnityEngine.Scripting;
using static ShopSessionData;

public class InventoryDataService : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static InventoryDataService Instance { get; private set; }

    // --- Firebase References ---
    private FirebaseFirestore db;
    private FirebaseUser currentUser;
    private string userProductsPath;
    private string userImportSlipsPath;

    // Listener cho Firebase
    private ListenerRegistration productListenerRegistration;

    // --- Operating Modes & Settings ---
    public bool IsCloudSyncEnabled { get; private set; } = false;
    public bool ManageImportPrice { get; private set; } = true;

    // --- Events ---
    public event Action<List<ProductData>> OnProductsLoaded;
    public event Action<bool> onSettingsChanged;

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
        if (productListenerRegistration != null)
        {
            productListenerRegistration.Dispose();
            productListenerRegistration = null;
        }
        FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
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
                userProductsPath = $"shops/{userId}/products";
                userImportSlipsPath = $"shops/{userId}/importSlips";
            }
            else
            {
                userProductsPath = null;
                userImportSlipsPath = null;
            }

            CheckOperatingModeAndSettings();
            LoadAndListenForProducts();
        }
    }

    public void CheckOperatingModeAndSettings()
    {
        string currentPackageName = CachedShopSettings?.packageType;

        bool hasCloudFeature = AppPackageConfig != null &&
                               AppPackageConfig.HasFeature(currentPackageName, AppFeature.Inventory);

        IsCloudSyncEnabled = hasCloudFeature;
        bool oldManagePrice = ManageImportPrice;
        ManageImportPrice = CachedShopSettings?.ManageImportPrice ?? true;

        if (oldManagePrice != ManageImportPrice)
        {
            onSettingsChanged?.Invoke(ManageImportPrice);
        }

        if (!IsCloudSyncEnabled)
        {
            Debug.LogWarning("DAL: Đang chạy ở chế độ LOCAL ONLY (BGDatabase).");
        }
    }

    // --- Logic Tải và Lắng nghe Sản phẩm ---

    public void LoadAndListenForProducts()
    {
        if (productListenerRegistration != null)
        {
            productListenerRegistration.Dispose();
            productListenerRegistration = null;
        }

        if (IsCloudSyncEnabled && currentUser != null && !string.IsNullOrEmpty(userProductsPath))
        {
            ListenForProductChanges(userProductsPath);
        }
        else
        {
            List<ProductData> localProducts = LoadProductsFromLocalDB();
            OnProductsLoaded?.Invoke(localProducts);
        }
    }

    [Preserve]
    public List<ProductData> LoadProductsFromLocalDB()
    {
        List<ProductData> products = new List<ProductData>();

        if (E_Product.MetaDefault == null) return products;

        E_Product.ForEachEntity(entity =>
        {
            products.Add(new ProductData
            {
                productId = entity.f_productId,
                productName = entity.f_name,
                unit = entity.f_unitOfProduct,
                price = entity.f_price,
                importPrice = entity.f_importPrice,
                barcode = entity.f_barcode,
                imageUrl = entity.f_imageUrl,
                stock = entity.f_stock,
                category = entity.f_category,
                manufacturer = entity.f_manufacturer
            });
        });
        return products;
    }

    private void ListenForProductChanges(string collectionPath)
    {
        CollectionReference productsRef = db.Collection(collectionPath);

        productListenerRegistration = productsRef.Listen(snapshot =>
        {
            Task.Run(() =>
            {
                List<ProductData> allProducts = new List<ProductData>();
                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        ProductData product = document.ConvertTo<ProductData>();
                        product.productId = document.Id;
                        allProducts.Add(product);
                    }
                }
                return allProducts;
            }).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    OnProductsLoaded?.Invoke(task.Result);
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"DAL: Lỗi khi lắng nghe thay đổi sản phẩm: {task.Exception.GetBaseException().Message}");
                }
            });
        });
    }

    // --- Logic CRUD Sản phẩm ---

    private void MapProductDataToEntity(ProductData data, E_Product entity)
    {
        entity.f_name = data.productName;
        entity.f_unitOfProduct = data.unit;
        entity.f_price = data.price;
        entity.f_importPrice = data.importPrice;
        entity.f_barcode = data.barcode;
        entity.f_imageUrl = data.imageUrl;
        entity.f_stock = (int)data.stock;
        entity.f_category = data.category;
        entity.f_manufacturer = data.manufacturer;
    }

    [Preserve]
    public async Task SaveProduct(ProductData product, bool isNew)
    {
        if (isNew && string.IsNullOrEmpty(product.productName))
        {
             throw new ArgumentException("Product name cannot be empty.");
        }

        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");

            CollectionReference productsRef = db.Collection(userProductsPath);
            DocumentReference docRef;

            if (isNew || string.IsNullOrEmpty(product.productId))
            {
                docRef = await productsRef.AddAsync(product);
                product.productId = docRef.Id;
            }
            else
            {
                docRef = productsRef.Document(product.productId);
                await docRef.SetAsync(product, SetOptions.MergeAll);
            }
        }
        else
        {
            if (E_Product.MetaDefault == null) throw new InvalidOperationException("BGDatabase E_Product MetaDefault is not initialized.");

            E_Product entity;

            if (isNew)
            {
                entity = E_Product.NewEntity();
                product.productId = Guid.NewGuid().ToString();
                entity.f_productId = product.productId;
                entity.f_name = product.productName;
            }
            else
            {
                entity = E_Product.FindEntity(e => e.f_productId == product.productId);
                if (entity == null) return;
            }

            MapProductDataToEntity(product, entity);
            BGRepo.I.Save();
            OnProductsLoaded?.Invoke(LoadProductsFromLocalDB());
        }
        await Task.CompletedTask;
    }

    [Preserve]
    public async Task DeleteProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId)) throw new ArgumentException("Product ID cannot be null or empty.");

        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");
            DocumentReference docRef = db.Collection(userProductsPath).Document(productId);
            await docRef.DeleteAsync();
        }
        else
        {
            if (E_Product.MetaDefault == null) throw new InvalidOperationException("BGDatabase E_Product MetaDefault is not initialized.");

            E_Product entityToDelete = E_Product.FindEntity(e => e.f_productId == productId);

            if (entityToDelete != null)
            {
                // FIX LỖI CS1501: Dùng DeleteEntity để xóa Entity trên MetaRow
                entityToDelete.Delete();

                BGRepo.I.Save();
                OnProductsLoaded?.Invoke(LoadProductsFromLocalDB());
            }
        }
        await Task.CompletedTask;
    }

    // --- Logic Lưu Phiếu Nhập Kho ---

    [Preserve]
    public async Task ProcessImportSlip(ImportSlipData slip)
    {
        if (slip == null || slip.items.Count == 0) return;

        if (!ManageImportPrice)
        {
            long totalValueAdjusted = 0;
            foreach (var item in slip.items)
            {
                item.importPrice = 0;
                totalValueAdjusted += item.quantity * item.importPrice;
            }
            slip.totalValue = totalValueAdjusted;
        }

        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");

            try
            {
                DocumentReference slipDocRef = await db.Collection(userImportSlipsPath).AddAsync(slip);
                slip.documentId = slipDocRef.Id;

                WriteBatch batch = db.StartBatch();
                CollectionReference productsRef = db.Collection(userProductsPath);

                foreach (var item in slip.items)
                {
                    DocumentReference productDocRef = productsRef.Document(item.productId);
                    Dictionary<string, object> updates = new Dictionary<string, object>
                    {
                        { "stock", FieldValue.Increment(item.quantity) }
                    };
                    batch.Update(productDocRef, updates);
                }

                await batch.CommitAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"DAL: Lỗi khi xử lý phiếu nhập trên Cloud: {e.Message}");
                throw;
            }
        }
        else
        {
            if (E_ImportSlip.MetaDefault == null) throw new InvalidOperationException("BGDatabase E_ImportSlip MetaDefault is not initialized.");
            if (E_Product.MetaDefault == null) throw new InvalidOperationException("BGDatabase E_Product MetaDefault is not initialized.");

            try
            {
                string internalSlipId = slip.slipId;

                foreach (var item in slip.items)
                {
                    E_ImportSlip newSlipEntity = E_ImportSlip.NewEntity();

                    newSlipEntity.f_slipId = internalSlipId;
                    newSlipEntity.f_date = slip.importDate.ToDateTime();
                    newSlipEntity.f_supplierId = slip.supplierId;

                    newSlipEntity.f_productId = item.productId;
                    newSlipEntity.f_quantity = (int)item.quantity;
                    newSlipEntity.f_importPrice = item.importPrice;

                    E_Product productEntity = E_Product.FindEntity(e => e.f_productId == item.productId);
                    if (productEntity != null)
                    {
                        long newStock = (long)productEntity.f_stock + item.quantity;
                        productEntity.f_stock = (int)Mathf.Min(newStock, int.MaxValue);
                    }
                }

                BGRepo.I.Save();
                OnProductsLoaded?.Invoke(LoadProductsFromLocalDB());
            }
            catch (Exception e)
            {
                Debug.LogError($"DAL: Lỗi khi xử lý phiếu nhập Local DB: {e.Message}");
                throw;
            }
        }
        await Task.CompletedTask;
    }

    // --- Logic Truy Vấn Phiếu Nhập ---

    // 1. Tải danh sách header của tất cả các phiếu nhập (cho màn hình lịch sử)
    [Preserve]
    public async Task<List<ImportSlipData>> GetImportSlipHeaders()
    {
        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) return new List<ImportSlipData>();

            QuerySnapshot snapshot = await db.Collection(userImportSlipsPath)
                .OrderByDescending("importDate")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                ImportSlipData slip = doc.ConvertTo<ImportSlipData>();
                slip.documentId = doc.Id;
                slip.items = null;
                return slip;
            }).ToList();
        }
        else
        {
            if (E_ImportSlip.MetaDefault == null) return new List<ImportSlipData>();

            // FIX LỖI CS1061: Sử dụng Entities property
            var groupedSlips = E_ImportSlip.FindEntities(entity => true)
                .GroupBy(e => e.f_slipId)
                .OrderByDescending(g => g.First().f_date);

            List<ImportSlipData> headers = new List<ImportSlipData>();

            foreach (var group in groupedSlips)
            {
                E_ImportSlip headerLine = group.First();
                long totalValue = group.Sum(line => line.f_quantity * line.f_importPrice);

                headers.Add(new ImportSlipData
                {
                    slipId = headerLine.f_slipId,
                    importDate = Timestamp.FromDateTime(headerLine.f_date),
                    supplierId = headerLine.f_supplierId,
                    totalValue = totalValue,
                    items = null
                });
            }
            return headers;
        }
    }

    // 2. Tải chi tiết một phiếu nhập (khi người dùng click vào một header)
    [Preserve]
    public async Task<ImportSlipData> GetSlipDetails(string slipId)
    {
        if (string.IsNullOrEmpty(slipId)) return null;

        if (IsCloudSyncEnabled)
        {
            if (currentUser == null) throw new InvalidOperationException("User not signed in for Cloud operation.");

            QuerySnapshot snapshot = await db.Collection(userImportSlipsPath)
                .WhereEqualTo("slipId", slipId)
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Documents.Count() > 0)
            {
                DocumentSnapshot doc = snapshot.Documents.First();
                ImportSlipData slip = doc.ConvertTo<ImportSlipData>();
                slip.documentId = doc.Id;
                return slip;
            }
            return null;
        }
        else
        {
            if (E_ImportSlip.MetaDefault == null) return null;

            List<E_ImportSlip> slipLines = E_ImportSlip.FindEntities(e => e.f_slipId == slipId);

            if (slipLines.Count == 0) return null;

            E_ImportSlip headerLine = slipLines[0];

            ImportSlipData slip = new ImportSlipData
            {
                slipId = headerLine.f_slipId,
                importDate = Timestamp.FromDateTime(headerLine.f_date),
                supplierId = headerLine.f_supplierId,
                items = new List<SlipItemData>()
            };

            long totalValue = 0;

            foreach (var line in slipLines)
            {
                SlipItemData item = new SlipItemData
                {
                    productId = line.f_productId,
                    productName = "Tên Sản Phẩm (Local Lookup Required)",
                    quantity = line.f_quantity,
                    importPrice = line.f_importPrice
                };
                slip.items.Add(item);
                totalValue += item.quantity * item.importPrice;
            }

            slip.totalValue = totalValue;
            return slip;
        }
    }

}
