// File: ImportSlipHistoryPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class ImportSlipHistoryPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelRoot; // GameObject gốc của panel
    public Button closeButton;
    public TMP_Text statusText;

    [Header("History List")]
    public Transform headerListContent; // Container chứa các mục Header
    public GameObject slipHeaderItemPrefab; // Prefab hiển thị thông tin tóm tắt một phiếu (Header)

    [Header("Detail Panel (Optional)")]
    public GameObject detailPanelRoot; // Panel hiển thị chi tiết phiếu
    public TMP_Text detailSlipIdText;
    public TMP_Text detailSupplierText;
    public Transform detailLineItemContainer; // Container chứa chi tiết sản phẩm

    void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
    }

    public void ShowPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        LoadHistory();
    }

    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private async void LoadHistory()
    {
        SetInteractable(false);
        statusText.text = "Đang tải lịch sử phiếu nhập...";

        // Dọn dẹp danh sách cũ
        foreach (Transform child in headerListContent)
        {
            Destroy(child.gameObject);
        }

        try
        {
            List<ImportSlipData> headers = await InventoryDataService.Instance.GetImportSlipHeaders();

            if (headers == null || headers.Count == 0)
            {
                statusText.text = "Chưa có phiếu nhập kho nào được ghi lại.";
                return;
            }

            foreach (var header in headers)
            {
                // Giả định slipHeaderItemPrefab có một script ImportSlipHeaderUI
                GameObject itemGO = Instantiate(slipHeaderItemPrefab, headerListContent);
                // Ví dụ: itemGO.GetComponent<ImportSlipHeaderUI>().SetData(header, OnHeaderClicked);

                // Tạm thời chỉ hiển thị Text
                itemGO.GetComponentInChildren<TMP_Text>().text =
                    $"{header.slipId} - Ngày: {header.importDate.ToDateTime():dd/MM/yyyy} - Tổng: {header.totalValue:N0} VNĐ";

                // Gán sự kiện cho nút/item để xem chi tiết
                Button button = itemGO.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnHeaderClicked(header.slipId));
                }
            }

            statusText.text = $"Hiển thị {headers.Count} phiếu nhập.";

        }
        catch (Exception e)
        {
            statusText.text = $"Lỗi tải lịch sử: {e.GetBaseException().Message}";
            Debug.LogError($"Lỗi tải lịch sử phiếu nhập: {e.Message}");
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private async void OnHeaderClicked(string slipId)
    {
        if (detailPanelRoot == null) return;

        detailPanelRoot.SetActive(true);
        detailSlipIdText.text = $"Đang tải chi tiết cho phiếu: {slipId}";

        // Dọn dẹp chi tiết cũ
        foreach (Transform child in detailLineItemContainer)
        {
            Destroy(child.gameObject);
        }

        try
        {
            ImportSlipData detail = await InventoryDataService.Instance.GetSlipDetails(slipId);

            if (detail != null)
            {
                detailSlipIdText.text = $"Phiếu: {detail.slipId} (Ngày: {detail.importDate.ToDateTime():dd/MM/yyyy})";
                detailSupplierText.text = $"Nhà cung cấp: {detail.supplierName}";

                // Hiển thị chi tiết từng sản phẩm
                foreach (var item in detail.items)
                {
                    // Giả định có một prefab đơn giản để hiển thị dòng chi tiết
                    GameObject lineGO = new GameObject("DetailLine");
                    lineGO.transform.SetParent(detailLineItemContainer, false);
                    lineGO.AddComponent<TMP_Text>().text =
                        $"{item.productName} | SL: {item.quantity:N0} | Giá nhập: {item.importPrice:N0} VNĐ";
                }
            }
            else
            {
                detailSlipIdText.text = $"Không tìm thấy chi tiết cho phiếu ID: {slipId}.";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi tải chi tiết phiếu: {e.Message}");
            detailSlipIdText.text = $"Lỗi khi tải chi tiết phiếu: {e.GetBaseException().Message}";
        }
    }

    private void SetInteractable(bool interactable)
    {
        // ... (Logic CanvasGroup cho panelRoot)
        if (panelRoot != null)
        {
            CanvasGroup canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }
}