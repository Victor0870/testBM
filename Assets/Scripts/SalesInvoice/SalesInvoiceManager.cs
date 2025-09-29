// File: Scripts/SalesInvoice/SalesInvoiceManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
// ĐÃ XÓA: using SalesDataService; // CS0138
using static ShopSessionData;
using static ShopSettingManager;
using UnityEngine.SceneManagement;

public class SalesInvoiceManager : MonoBehaviour
{
    // --- UI DANH SÁCH ---
    [Header("UI List References")]
    public TMP_Text statusText;
    public TMP_InputField searchInputField;
    public Button goToHomepageButton;

    [Header("Sales List Display")]
    public Transform saleListContentParent;
    public GameObject saleItemPrefab; // Cần SaleOrderUIItem.cs (Mục I)

    // --- UI CHI TIẾT ---
    [Header("Detail Panel References")]
    public SaleOrderDetailPanel detailPanelManager; // Cần SaleOrderDetailPanel.cs (Mục J)

    private List<SaleData> allSales = new List<SaleData>();

    void Start()
    {
        InitializeManager();
        SetupUIListeners();
    }

    void OnDestroy()
    {
        if (SalesDataService.Instance != null)
        {
            SalesDataService.Instance.OnSalesLoaded -= HandleSalesLoaded;
        }
    }

    private void InitializeManager()
    {
        if (SalesDataService.Instance == null)
        {
            statusText.text = "Lỗi: SalesDataService chưa được khởi tạo.";
            Debug.LogError("SalesInvoiceManager: SalesDataService is null.");
            return;
        }

        // 1. Kiểm tra trạng thái Cloud Sync
        SalesDataService.Instance.CheckCloudSyncStatus();

        // 2. Đăng ký lắng nghe sự kiện tải dữ liệu
        SalesDataService.Instance.OnSalesLoaded += HandleSalesLoaded;

        // 3. Yêu cầu tải dữ liệu ban đầu
        LoadSales();
    }

    private void SetupUIListeners()
    {
        goToHomepageButton?.onClick.AddListener(() => SceneManager.LoadScene("Homepage"));

        // Listener cho tìm kiếm
        searchInputField?.onValueChanged.AddListener(OnSearchValueChanged);
    }

    private void LoadSales()
    {
        statusText.text = "Đang tải lịch sử đơn hàng...";
        // Yêu cầu Data Service tải dữ liệu từ Cloud (nếu Pro) hoặc Local (nếu Basic)
        SalesDataService.Instance.LoadAndListenForSales();
    }

    private void HandleSalesLoaded(List<SaleData> sales)
    {
        allSales = sales;
        UpdateSalesUI(allSales);
    }

    private void OnSearchValueChanged(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            UpdateSalesUI(allSales);
            return;
        }

        string lowerSearchText = searchText.ToLower();
        var filteredList = allSales.Where(s =>
            (s.customerName != null && s.customerName.ToLower().Contains(lowerSearchText)) ||
            (s.customerPhone != null && s.customerPhone.Contains(lowerSearchText)) ||
            (s.saleId != null && s.saleId.ToLower().Contains(lowerSearchText))
        ).ToList();

        UpdateSalesUI(filteredList);
    }

    private void UpdateSalesUI(List<SaleData> salesToDisplay)
    {
        // Xóa các item cũ
        foreach (Transform child in saleListContentParent)
        {
            Destroy(child.gameObject);
        }

        if (salesToDisplay == null || salesToDisplay.Count == 0)
        {
            statusText.text = "Không tìm thấy đơn hàng nào.";
            return;
        }

        foreach (var sale in salesToDisplay.OrderByDescending(s => s.saleDate))
        {
            if (saleItemPrefab != null)
            {
                GameObject itemGO = Instantiate(saleItemPrefab, saleListContentParent);
                SaleOrderUIItem uiItem = itemGO.GetComponent<SaleOrderUIItem>();
                if (uiItem != null)
                {
                    // Truyền dữ liệu và callback để mở Panel chi tiết
                    uiItem.SetSaleData(sale, OnViewDetailsRequest);
                }
            }
        }

        statusText.text = $"Hiển thị {salesToDisplay.Count} đơn hàng (Tổng: {allSales.Count}).";
    }

    // Callback được gọi từ SaleOrderUIItem khi người dùng nhấn nút "Xem chi tiết"
    private void OnViewDetailsRequest(SaleData saleToView)
    {
        if (detailPanelManager != null)
        {
            detailPanelManager.ShowPanel(saleToView);
        }
        else
        {
            Debug.LogError("SaleOrderDetailPanel chưa được gán trong Inspector.");
            StatusPopupManager.Instance.ShowPopup("Lỗi: Không tìm thấy giao diện chi tiết đơn hàng.");
        }
    }
}