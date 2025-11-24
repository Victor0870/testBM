// File: Scripts/Inventory/ProductSelectionPopupManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

public class ProductSelectionPopupManager : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject panelRoot;
    public TMP_InputField searchInputField;
    public Transform contentParent;
    public GameObject productItemPrefab; // Prefab item trong danh sách chọn
    public Button closeButton;

    // Callback: ProductData được chọn
    private Action<ProductData> _onProductSelectedCallback;
    private List<ProductData> _allProducts = new List<ProductData>();
    
    // Danh sách ID sản phẩm đã có trong phiếu (dùng để lọc)
    private List<string> _excludeProductIds = new List<string>();


    void Awake()
    {
        panelRoot?.SetActive(false);
        closeButton?.onClick.AddListener(HideSelection);
        searchInputField?.onValueChanged.AddListener(OnSearchValueChanged);
    }

    // ShowSelection được gọi từ ImportSlipCreationPanelManager
    public void ShowSelection(List<string> currentSlipProductIds, Action<ProductData> onProductSelected)
    {
        _onProductSelectedCallback = onProductSelected;
        _excludeProductIds = currentSlipProductIds; // Lưu lại danh sách ID cần loại trừ

        // GIẢ ĐỊNH CÁCH LẤY TẤT CẢ SẢN PHẨM:
        // Cần chỉnh sửa InventoryDataService để có một public method trả về List<ProductData>
        if (InventoryDataService.Instance != null)
        {
            _allProducts = InventoryDataService.Instance.LoadProductsFromLocalDB();
        }
        
        RenderProductList(null); // Hiển thị tất cả ban đầu
        searchInputField.text = "";
        panelRoot?.SetActive(true);
    }

    public void HideSelection()
    {
        panelRoot?.SetActive(false);
    }

    private void OnSearchValueChanged(string searchText)
    {
        RenderProductList(searchText);
    }
    
    private void HandleProductItemClicked(ProductData product)
    {
        _onProductSelectedCallback?.Invoke(product);
        HideSelection();
    }
    
    private void RenderProductList(string searchText)
    {
        // Xóa các mục cũ
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        IEnumerable<ProductData> displayList = _allProducts;

        // 1. Lọc theo tìm kiếm
        if (!string.IsNullOrEmpty(searchText))
        {
            string lowerSearchText = searchText.ToLower();
             displayList = displayList.Where(p => 
                (p.productName?.ToLower().Contains(lowerSearchText) ?? false) ||
                (p.barcode?.ToLower().Contains(lowerSearchText) ?? false));
        }
        
        // 2. Lọc loại trừ sản phẩm đã có trong phiếu
        displayList = displayList.Where(p => !_excludeProductIds.Contains(p.productId));

        // Render từng mục
        foreach (var product in displayList.OrderBy(p => p.productName))
        {
            if (productItemPrefab != null)
            {
                 GameObject itemGO = Instantiate(productItemPrefab, contentParent);
                 
                 // Giả định productItemPrefab có TMP_Text và Button
                 itemGO.GetComponentInChildren<TMP_Text>().text = $"{product.productName} ({product.stock:N0})";
                 itemGO.GetComponent<Button>().onClick.AddListener(() => HandleProductItemClicked(product));
            }
        }
    }
}
