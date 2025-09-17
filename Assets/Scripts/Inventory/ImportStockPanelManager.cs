// File: ImportStockPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class ImportStockPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelRoot;
    public TMP_Text productNameText;
    public TMP_Text currentStockText;
    public TMP_InputField importQuantityInputField;
    public TMP_Text importPriceDisplay;
    public Button confirmButton;
    public Button cancelButton;
    public TMP_Text statusMessageText;

    private FirebaseFirestore db;
    private Firebase.Auth.FirebaseUser currentUser;
    private CollectionReference userProductsCollection;

    private ProductData productToUpdate;
    private Action onStockUpdatedCallback;

    void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // DÒNG NÀY SẼ GỌI ĐẾN PHƯƠNG THỨC MÀ TA SẼ ĐỔI TÊN
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmImportButtonClicked); // ĐÚNG TÊN
        if (cancelButton != null) cancelButton.onClick.AddListener(HidePanel);

        db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
    }

    void OnDestroy()
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        Firebase.Auth.FirebaseUser newUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                Debug.Log($"ImportStockPanelManager: UserProductsCollection set for UID: {currentUser.UserId}");
            }
            else
            {
                userProductsCollection = null;
                Debug.Log("ImportStockPanelManager: User logged out.");
            }
        }
    }

    public void ShowPanel(ProductData product, Action callback)
    {
        if (product == null)
        {
            Debug.LogError("ProductData is null when trying to show import panel.");
            return;
        }

        productToUpdate = product;
        onStockUpdatedCallback = callback;

        if (productNameText != null) productNameText.text = product.productName;
        if (currentStockText != null) currentStockText.text = $"Tồn kho hiện tại: {product.stock:N0}";
        if (importQuantityInputField != null) importQuantityInputField.text = "";
        if (importPriceDisplay != null) importPriceDisplay.text = $"Giá nhập: {product.importPrice:N0} VNĐ";
        if (statusMessageText != null) statusMessageText.text = "";

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        SetInteractable(true);
    }

    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        statusMessageText.text = "";
        SetInteractable(false);
    }

    public void SetInteractable(bool interactable)
    {
        if (panelRoot != null)
        {
            CanvasGroup canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.7f;
        }

        if (importQuantityInputField != null) importQuantityInputField.interactable = interactable;
        if (confirmButton != null) confirmButton.interactable = interactable;
        if (cancelButton != null) cancelButton.interactable = interactable;
    }

    // ĐỔI TÊN PHƯƠNG THỨC TẠI ĐÂY
    private async void OnConfirmImportButtonClicked() // ĐỔI TÊN TỪ OnConfirmAddButtonClicked THÀNH OnConfirmImportButtonClicked
    {
        if (productToUpdate == null || string.IsNullOrEmpty(productToUpdate.productId))
        {
            Debug.LogError("Không có sản phẩm nào được chọn hoặc thiếu ProductId.");
            if (statusMessageText != null) statusMessageText.text = "Lỗi: Không tìm thấy thông tin sản phẩm.";
            return;
        }

        if (userProductsCollection == null)
        {
            Debug.LogError("UserProductsCollection chưa được thiết lập. Người dùng chưa đăng nhập?");
            if (statusMessageText != null) statusMessageText.text = "Lỗi: Vui lòng đăng nhập.";
            return;
        }

        if (!long.TryParse(importQuantityInputField.text, out long quantityToAdd) || quantityToAdd <= 0)
        {
            if (statusMessageText != null) statusMessageText.text = "Vui lòng nhập số lượng hợp lệ (> 0).";
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            return;
        }


        if (statusMessageText != null) statusMessageText.text = "Đang cập nhật tồn kho...";
        SetInteractable(false);

        DocumentReference productDocRef = userProductsCollection.Document(productToUpdate.productId);

        try
        {
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "stock", FieldValue.Increment(quantityToAdd) }
            };

            await productDocRef.UpdateAsync(updates);

            productToUpdate.stock += quantityToAdd;

            Debug.Log($"Đã cập nhật tồn kho cho sản phẩm '{productToUpdate.productName}'. Thêm: {quantityToAdd}, Tồn kho mới: {productToUpdate.stock}");
            if (statusMessageText != null) statusMessageText.text = "Cập nhật tồn kho thành công!";

            onStockUpdatedCallback?.Invoke();

            await Task.Delay(1000);
            HidePanel();
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi cập nhật tồn kho cho sản phẩm '{productToUpdate.productName}': {e.Message}");
            if (statusMessageText != null) statusMessageText.text = $"Lỗi cập nhật: {e.Message}";
        }
        finally
        {
            SetInteractable(true);
        }
    }
}