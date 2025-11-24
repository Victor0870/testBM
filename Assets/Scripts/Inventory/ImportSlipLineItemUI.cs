// File: Scripts/Inventory/ImportSlipLineItemUI.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class ImportSlipLineItemUI : MonoBehaviour
{
    [Header("UI Fields")]
    public TMP_Text productNameText;
    public TMP_InputField quantityInputField;
    public TMP_InputField priceInputField; // Giá nhập
    public Button removeButton;

    private SlipItemData _data;
    private Action<string, GameObject> _onRemove;
    private Action<string, long, long> _onDataChanged; // ID, newQuantity, newPrice

    public void SetData(SlipItemData data, bool managePrice, Action<string, GameObject> onRemove, Action<string, long, long> onDataChanged)
    {
        _data = data;
        _onRemove = onRemove;
        _onDataChanged = onDataChanged;

        productNameText.text = data.productName;
        quantityInputField.text = data.quantity.ToString();
        priceInputField.text = data.importPrice.ToString();
        
        // Ẩn/hiện và thiết lập Input Price
        priceInputField.gameObject.SetActive(managePrice);
        if (!managePrice) priceInputField.interactable = false;

        // Gán Listeners
        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveClicked);

        quantityInputField.onEndEdit.RemoveAllListeners();
        quantityInputField.onEndEdit.AddListener(OnQuantityEndEdit);
        
        priceInputField.onEndEdit.RemoveAllListeners();
        priceInputField.onEndEdit.AddListener(OnPriceEndEdit);
    }
    
    private void OnQuantityEndEdit(string value)
    {
        if (long.TryParse(value, out long newQuantity) && newQuantity > 0)
        {
            _data.quantity = newQuantity;
            _onDataChanged?.Invoke(_data.productId, _data.quantity, _data.importPrice);
        }
        else
        {
             quantityInputField.text = _data.quantity.ToString();
             StatusPopupManager.Instance.ShowPopup("Số lượng phải lớn hơn 0.");
        }
    }
    
    private void OnPriceEndEdit(string value)
    {
        if (long.TryParse(value, out long newPrice) && newPrice >= 0)
        {
            _data.importPrice = newPrice;
            _onDataChanged?.Invoke(_data.productId, _data.quantity, _data.importPrice);
        }
        else
        {
             priceInputField.text = _data.importPrice.ToString();
             StatusPopupManager.Instance.ShowPopup("Giá nhập phải là số không âm.");
        }
    }

    private void OnRemoveClicked()
    {
        _onRemove?.Invoke(_data.productId, this.gameObject);
    }
}
