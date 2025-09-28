// File: ImportSlipCreationPanelManager.cs
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

    [Header("Product Line Items")]
    public Transform lineItemContainer; // Transform chứa danh sách các sản phẩm trong phiếu
    public GameObject productItemLinePrefab; // Prefab của một dòng sản phẩm (để thêm vào container)
    public Button addProductButton; // Nút để mở cửa sổ chọn/tìm kiếm sản phẩm

    [Header("Quick Add Product Info (Optional)")]
    public TMP_Text quickProductNameText; // Hiển thị tên sản phẩm nếu dùng Quick Add
    public TMP_InputField quickAddQuantityInput;
    public TMP_InputField quickAddImportPriceInput; // Trường này sẽ bị vô hiệu hóa/ẩn nếu ManageImportPrice = false

    private Action onSlipCreatedCallback;

    // Dữ liệu tạm thời của phiếu đang được tạo
    private ImportSlipData currentSlip = new ImportSlipData();
    private Dictionary<string, SlipItemData> slipItemsMap = new Dictionary<string, SlipItemData>(); // Key: ProductId

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (confirmAddSlipButton != null) confirmAddSlipButton.onClick.AddListener(async () => await OnConfirmAddSlipButtonClicked());
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        // if (addProductButton != null) addProductButton.onClick.AddListener(OpenProductSelectionPanel); // Logic phức tạp, chỉ làm mẫu

        // Theo dõi sự kiện thay đổi setting để cập nhật UI
        InventoryDataService.Instance.onSettingsChanged += UpdateImportPriceUI;
    }

    void OnDestroy()
    {
        // Gỡ bỏ listener để tránh lỗi
        if (InventoryDataService.Instance != null)
        {
            InventoryDataService.Instance.onSettingsChanged -= UpdateImportPriceUI;
        }
    }

    // Hàm này được gọi từ InventoryManager
    public void ShowPanel(ProductData quickAddProduct = null, Action callback = null)
    {
        onSlipCreatedCallback = callback;
        ResetPanel();

        // Tạo Slip ID tự động (ví dụ: SIP-20250928-001)
        slipIdInputField.text = $"SIP-{DateTime.Now:yyyyMMdd}-{UnityEngine.Random.Range(100, 999)}";

        if (quickAddProduct != null)
        {
            // Chế độ Quick Add (tạo phiếu 1 sản phẩm)
            quickProductNameText.text = $"Nhập kho nhanh: {quickAddProduct.productName}";

            // Tạm thời thêm sản phẩm vào slipItemsMap để xử lý khi nhấn Confirm
            SlipItemData quickItem = new SlipItemData
            {
                productId = quickAddProduct.productId,
                productName = quickAddProduct.productName,
                importPrice = quickAddProduct.importPrice,
                quantity = 0
            };
            slipItemsMap[quickAddProduct.productId] = quickItem;

            // Vô hiệu hóa/Ẩn các UI không liên quan đến Quick Add
            lineItemContainer.gameObject.SetActive(false);
        }
        else
        {
            // Chế độ tạo phiếu hàng loạt
            quickProductNameText.text = "Tạo phiếu nhập kho mới";
            lineItemContainer.gameObject.SetActive(true);
        }

        UpdateImportPriceUI(InventoryDataService.Instance.ManageImportPrice);
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void UpdateImportPriceUI(bool managePrice)
    {
        // Vô hiệu hóa/Ẩn trường Giá nhập nếu setting tắt
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

        if (slipIdInputField != null) slipIdInputField.text = string.Empty;
        if (supplierNameInputField != null) supplierNameInputField.text = string.Empty;
        if (quickProductNameText != null) quickProductNameText.text = string.Empty;
        if (quickAddQuantityInput != null) quickAddQuantityInput.text = "0";
        if (quickAddImportPriceInput != null) quickAddImportPriceInput.text = "0";

        // Xóa tất cả Line Items cũ (nếu có)
        foreach (Transform child in lineItemContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

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

        // 3. Thu thập dữ liệu Line Items (dùng Quick Add nếu đang ở chế độ Quick Add)
        if (lineItemContainer.gameObject.activeSelf == false) // Chế độ Quick Add
        {
            if (slipItemsMap.Count != 1) { /* Lỗi */ SetInteractable(true); return; }

            var quickItem = slipItemsMap.Values.First(); // Lấy sản phẩm duy nhất

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
            // Cập nhật dữ liệu vào quickItem
            quickItem.quantity = quantity;
            quickItem.importPrice = importPrice;
            currentSlip.items.Add(quickItem);
        }
        else
        {
            // TODO: Logic thu thập dữ liệu từ các dòng sản phẩm trong lineItemContainer (Chế độ hàng loạt)
            // Ví dụ: Lặp qua slipItemsMap (nếu bạn dùng map để quản lý data) hoặc đọc từ các Component UI Line Item
             if (slipItemsMap.Count == 0)
             {
                 StatusPopupManager.Instance.ShowPopup("Phiếu nhập phải có ít nhất một sản phẩm.");
                 SetInteractable(true);
                 return;
             }
             currentSlip.items.AddRange(slipItemsMap.Values); // Giả sử slipItemsMap đã được điền đủ
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
    }

    // TODO: Bổ sung logic OpenProductSelectionPanel và AddProductToSlipUI khi cần
}