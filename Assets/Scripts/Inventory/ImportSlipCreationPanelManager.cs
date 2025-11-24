// File: Scripts/Inventory/ImportSlipCreationPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

// Gán tên lớp mới để thay thế ImportStockPanelManager cũ
public class ImportSlipCreationPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelRoot; // GameObject gốc của panel
    public Button closeButton;
    public Button confirmAddSlipButton;

    [Header("Slip Header Inputs")]
    public TMP_InputField slipIdInputField; // Số phiếu (Internal Key)
    public TMP_InputField supplierNameInputField; // Tên nhà cung cấp
    //public TMP_Dropdown supplierDropdown; // Tùy chọn: dùng dropdown chọn nhà cung cấp

    [Header("Batch Mode Components")]
    public ProductSelectionPopupManager productSelectionPopupManager; // Manager của Popup Chọn Sản phẩm
    public TMP_Text totalSlipValueText; // Hiển thị tổng giá trị phiếu

    [Header("Product Line Items")]
    public Transform lineItemContainer; // Transform chứa danh sách các sản phẩm trong phiếu
    public GameObject productItemLinePrefab; // Prefab của một dòng sản phẩm (để thêm vào container)
    public Button addProductButton; // Nút để mở cửa sổ chọn/tìm kiếm sản phẩm

    [Header("Quick Add Product Info (Optional)")]
    public TMP_Text quickProductNameText; // Hiển thị tên sản phẩm nếu dùng Quick Add
    public TMP_InputField quickAddQuantityInput;
    public TMP_InputField quickAddImportPriceInput; // Trường này sẽ bị vô hiệu hóa/ẩn nếu ManageImportPrice = false

    private Action onSlipCreatedCallback;

    // THAM CHIẾU MỚI: Để giữ callback từ InventoryManager (mở AddProductPanel)
    private Action _onAddNewProductRequestedByIM; 

    // Dữ liệu tạm thời của phiếu đang được tạo
    private ImportSlipData currentSlip = new ImportSlipData();
    // Key: ProductId. Value: SlipItemData (bao gồm quantity và importPrice mới nhất)
    private Dictionary<string, SlipItemData> slipItemsMap = new Dictionary<string, SlipItemData>(); 
    // Dictionary để quản lý GameObject UI của từng dòng sản phẩm (để dễ dàng xóa)
    private Dictionary<string, GameObject> slipItemUIObjects = new Dictionary<string, GameObject>();

    // --- MỚI: HÀM TẠO CHUỖI NGẪU NHIÊN 4 KÝ TỰ ---
    private string GenerateRandomAlphanumeric(int length)
    {
        // 26 chữ thường + 26 chữ hoa + 10 số = 62 ký tự
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        // Sử dụng System.Random để đảm bảo tính ngẫu nhiên
        var random = new System.Random(); 
        
        return new string(Enumerable.Repeat(chars, length)
          .Select(s => s[random.Next(s.Length)]).ToArray());
    }
    // ---------------------------------------------


    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (confirmAddSlipButton != null) confirmAddSlipButton.onClick.AddListener(async () => await OnConfirmAddSlipButtonClicked());
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        
        // Gán listener cho nút thêm sản phẩm (Batch Mode)
        if (addProductButton != null) addProductButton.onClick.AddListener(OpenProductSelectionPanel);

        // Theo dõi sự kiện thay đổi setting để cập nhật UI
        InventoryDataService.Instance.onSettingsChanged += UpdateImportPriceUI;
        
        // Cập nhật tổng tiền ban đầu
        UpdateTotalSlipValueUI();
    }

    void OnDestroy()
    {
        // Gỡ bỏ listener để tránh lỗi
        if (InventoryDataService.Instance != null)
        {
            InventoryDataService.Instance.onSettingsChanged -= UpdateImportPriceUI;
        }
    }

    // THAY ĐỔI: Thêm tham số callback mới
    public void ShowPanel(ProductData quickAddProduct = null, Action callback = null, Action onAddNewProductRequestedByIM = null)
    {
        onSlipCreatedCallback = callback;
        _onAddNewProductRequestedByIM = onAddNewProductRequestedByIM; // <-- LƯU CALLBACK
        ResetPanel();

        // --- SỬA ĐỔI TẠI ĐÂY: TẠO SLIP ID 4 KÝ TỰ ---
        slipIdInputField.text = $"SIP-{DateTime.Now:yyyyMMdd}-{GenerateRandomAlphanumeric(4)}";

        // Vô hiệu hóa nút "Thêm sản phẩm" nếu ở chế độ Quick Add
        if (addProductButton != null) addProductButton.interactable = (quickAddProduct == null);

        // Cấu hình chế độ Quick Add (Single Product)
        if (quickAddProduct != null)
        {
            quickProductNameText.gameObject.SetActive(true);
            quickProductNameText.text = $"Nhập kho nhanh: {quickAddProduct.productName}";
            lineItemContainer.gameObject.SetActive(false); // Ẩn container hàng loạt

            // Thêm sản phẩm vào map (sẽ được xử lý trong OnConfirmAddSlipButtonClicked)
            SlipItemData quickItem = new SlipItemData
            {
                productId = quickAddProduct.productId,
                productName = quickAddProduct.productName,
                importPrice = quickAddProduct.importPrice,
                quantity = 0
            };
            slipItemsMap.Add(quickAddProduct.productId, quickItem);
        }
        // Cấu hình chế độ Batch (Multiple Products)
        else
        {
            quickProductNameText.gameObject.SetActive(false);
            lineItemContainer.gameObject.SetActive(true); // Hiển thị container hàng loạt
        }

        UpdateImportPriceUI(InventoryDataService.Instance.ManageImportPrice);
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void UpdateImportPriceUI(bool managePrice)
    {
        // Vô hiệu hóa/Ẩn trường Giá nhập nếu setting tắt (cho Quick Add)
        if (quickAddImportPriceInput != null)
        {
            quickAddImportPriceInput.gameObject.SetActive(managePrice);
            if (!managePrice)
            {
                quickAddImportPriceInput.text = "0"; // Đảm bảo giá trị là 0
            }
        }
    }

    private void ResetPanel()
    {
        currentSlip = new ImportSlipData();
        slipItemsMap.Clear();
        
        // Xóa tất cả GameObject dòng sản phẩm UI
        foreach (Transform child in lineItemContainer)
        {
            Destroy(child.gameObject);
        }
        slipItemUIObjects.Clear();

        if (slipIdInputField != null) slipIdInputField.text = string.Empty;
        if (supplierNameInputField != null) supplierNameInputField.text = string.Empty;
        if (quickProductNameText != null) quickProductNameText.text = string.Empty;
        if (quickAddQuantityInput != null) quickAddQuantityInput.text = "0";
        if (quickAddImportPriceInput != null) quickAddImportPriceInput.text = "0";
        
        UpdateTotalSlipValueUI();
    }

    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        // Đảm bảo ẩn Popup chọn sản phẩm
        if (productSelectionPopupManager != null) 
        {
             productSelectionPopupManager.HideSelection();
        }
    }
    
    // --- MỚI: CẬP NHẬT TỔNG GIÁ TRỊ PHIẾU ---
    private void UpdateTotalSlipValueUI()
    {
        // Chỉ tính tổng tiền cho chế độ hàng loạt (vì Quick Add không hiển thị Line Item)
        long totalValue = slipItemsMap.Values.Sum(item => item.quantity * item.importPrice);
        currentSlip.totalValue = totalValue; 
        
        if (totalSlipValueText != null)
        {
            totalSlipValueText.text = $"Tổng giá trị: {totalValue:N0} VNĐ";
        }
    }
    
    // --- MỚI: LOGIC OpenProductSelectionPanel (Batch Mode) ---
    public void OpenProductSelectionPanel() // HÀM THAY THẾ TODO
    {
        if (productSelectionPopupManager == null)
        {
            StatusPopupManager.Instance.ShowPopup("Lỗi: Component chọn sản phẩm chưa được gán.");
            return;
        }
        
        // Hiển thị Popup và truyền callback
        productSelectionPopupManager.ShowSelection(
            slipItemsMap.Keys.ToList(), 
            HandleProductSelectedForSlip,
            _onAddNewProductRequestedByIM); // <-- TRUYỀN CALLBACK MỞ PANEL MỚI
    }
    
    // --- MỚI: CALLBACK KHI SẢN PHẨM ĐƯỢC CHỌN TỪ POPUP (Batch Mode) ---
    private void HandleProductSelectedForSlip(ProductData product)
    {
        if (slipItemsMap.ContainsKey(product.productId))
        {
            StatusPopupManager.Instance.ShowPopup($"Sản phẩm '{product.productName}' đã có trong phiếu. Vui lòng chỉnh sửa số lượng.");
            return;
        }
        
        // Tạo dữ liệu dòng sản phẩm mới
        SlipItemData newSlipItem = new SlipItemData
        {
            productId = product.productId,
            productName = product.productName,
            quantity = 1, // Mặc định là 1 khi thêm
            importPrice = product.importPrice // Sử dụng giá nhập mặc định của sản phẩm
        };
        
        slipItemsMap.Add(product.productId, newSlipItem);
        
        // Gọi hàm để render/cập nhật UI (RenderSlipLineItem)
        RenderSlipLineItem(newSlipItem);
        UpdateTotalSlipValueUI();
    }
    
    // --- MỚI: RENDER DÒNG SẢN PHẨM TRONG PHIẾU (HÀM THAY THẾ TODO) ---
    private void RenderSlipLineItem(SlipItemData slipItem)
    {
        if (productItemLinePrefab == null || lineItemContainer == null)
        {
            Debug.LogError("Thiếu Prefab Line Item hoặc Container.");
            return;
        }

        GameObject itemGO = Instantiate(productItemLinePrefab, lineItemContainer);
        // Cần đảm bảo ImportSlipLineItemUI.cs đã được tạo và gán vào productItemLinePrefab
        ImportSlipLineItemUI uiItem = itemGO.GetComponent<ImportSlipLineItemUI>(); 
        
        if (uiItem != null)
        {
            // Thiết lập trạng thái Input Price
            bool managePrice = InventoryDataService.Instance.ManageImportPrice;
            
            uiItem.SetData(slipItem, 
                           managePrice,
                           // On Remove Callback
                           (productId, gameObject) => { 
                               HandleRemoveSlipItem(productId, gameObject);
                           }, 
                           // On Quantity/Price Change Callback
                           (productId, newQuantity, newPrice) => {
                               HandleSlipItemDataChanged(productId, newQuantity, newPrice);
                           });
                           
            slipItemUIObjects.Add(slipItem.productId, itemGO);
        }
    }
    
    // --- MỚI: CALLBACK KHI SỐ LƯỢNG/GIÁ THAY ĐỔI TRÊN DÒNG UI ---
    private void HandleSlipItemDataChanged(string productId, long newQuantity, long newPrice)
    {
        if (slipItemsMap.ContainsKey(productId))
        {
            slipItemsMap[productId].quantity = newQuantity;
            slipItemsMap[productId].importPrice = newPrice; // Sẽ là 0 nếu ManageImportPrice tắt
            
            UpdateTotalSlipValueUI(); // Tính toán lại tổng tiền
        }
    }

    // --- MỚI: CALLBACK KHI XÓA DÒNG SẢN PHẨM ---
    private void HandleRemoveSlipItem(string productId, GameObject itemGO)
    {
        if (slipItemsMap.ContainsKey(productId))
        {
            slipItemsMap.Remove(productId);
            if (slipItemUIObjects.ContainsKey(productId))
            {
                slipItemUIObjects.Remove(productId);
            }
            Destroy(itemGO);
            
            UpdateTotalSlipValueUI(); // Tính toán lại tổng tiền
        }
    }
    
    // --- CẬP NHẬT: OnConfirmAddSlipButtonClicked ---
    private async Task OnConfirmAddSlipButtonClicked()
    {
        SetInteractable(false);

        // 1. Validation cơ bản
        if (string.IsNullOrEmpty(slipIdInputField.text))
        {
            StatusPopupManager.Instance.ShowPopup("Số phiếu không được để trống.");
            SetInteractable(true);
            return;
        }

        // 2. Thu thập dữ liệu Header
        currentSlip.slipId = slipIdInputField.text.Trim();
        currentSlip.supplierName = supplierNameInputField.text.Trim();
        currentSlip.importDate = Firebase.Firestore.Timestamp.FromDateTime(DateTime.UtcNow);

        // 3. Thu thập dữ liệu Line Items 
        if (lineItemContainer.gameObject.activeSelf == false) // Chế độ Quick Add
        {
            if (slipItemsMap.Count != 1) { /* Lỗi */ SetInteractable(true); return; }

            var quickItem = slipItemsMap.Values.First(); 

            if (!long.TryParse(quickAddQuantityInput.text, out long quantity) || quantity <= 0)
            {
                StatusPopupManager.Instance.ShowPopup("Vui lòng nhập số lượng nhập hợp lệ.");
                SetInteractable(true);
                return;
            }

            long importPrice = 0;
            // Chỉ đọc giá nhập nếu trường này active/visible
            if (quickAddImportPriceInput.gameObject.activeSelf)
            {
                 if (!long.TryParse(quickAddImportPriceInput.text, out importPrice) || importPrice < 0)
                 {
                    StatusPopupManager.Instance.ShowPopup("Giá nhập không hợp lệ.");
                    SetInteractable(true);
                    return;
                 }
            }
            quickItem.quantity = quantity;
            quickItem.importPrice = importPrice;
            currentSlip.items.Clear();
            currentSlip.items.Add(quickItem);
        }
        else // Chế độ hàng loạt (Batch Mode)
        {
             if (slipItemsMap.Count == 0)
             {
                 StatusPopupManager.Instance.ShowPopup("Phiếu nhập phải có ít nhất một sản phẩm.");
                 SetInteractable(true);
                 return;
             }
             currentSlip.items.Clear();
             currentSlip.items.AddRange(slipItemsMap.Values); 
        }

        // 4. Gọi Data Service để xử lý nghiệp vụ
        try
        {
            StatusPopupManager.Instance.ShowPopup("Đang tiến hành tạo phiếu nhập và cập nhật tồn kho...");
            await InventoryDataService.Instance.ProcessImportSlip(currentSlip);

            StatusPopupManager.Instance.ShowPopup("Tạo phiếu nhập và cập nhật tồn kho thành công!");
            onSlipCreatedCallback?.Invoke();
            HidePanel();
        }
        catch (Exception e)
        {
            StatusPopupManager.Instance.ShowPopup($"Lỗi xử lý phiếu nhập: {e.GetBaseException().Message}");
            Debug.LogError($"Lỗi khi ProcessImportSlip: {e.Message}");
        }
        finally
        {
            SetInteractable(true);
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (panelRoot != null)
        {
            CanvasGroup canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.7f;
        }

        // Vô hiệu hóa nút Confirm
        if (confirmAddSlipButton != null) confirmAddSlipButton.interactable = interactable;
        if (closeButton != null) closeButton.interactable = interactable;
        
        // Nút thêm sản phẩm cũng bị vô hiệu hóa khi đang xử lý
        if (addProductButton != null) addProductButton.interactable = interactable;
    }
}
