// File: Scripts/Customer/CustomerUIItem.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using UnityEngine.Events;

public class CustomerUIItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text nameText;
    public TMP_Text phoneText;
    public TMP_Text addressText;
    public TMP_Text taxIdText;      // Thêm hiển thị Mã số thuế
    public Button editButton;

    private CustomerData currentCustomerData;
    private Action<CustomerData> onEditRequested;

    void Awake()
    {
        // Gán sự kiện cho nút chỉnh sửa
        editButton?.onClick.AddListener(OnEditButtonClicked);
    }

    // Phương thức thiết lập dữ liệu cho UI Item
    public void SetCustomerData(CustomerData data, Action<CustomerData> editCallback)
    {
        currentCustomerData = data;
        onEditRequested = editCallback;

        if (nameText != null) nameText.text = data.name ?? "N/A";
        if (phoneText != null) phoneText.text = data.phone ?? "N/A";

        // Hiển thị địa chỉ (nếu có)
        if (addressText != null)
        {
            string displayAddress = string.IsNullOrEmpty(data.address) ? "Địa chỉ: N/A" : $"Địa chỉ: {data.address}";
            addressText.text = displayAddress;
        }

        // Hiển thị Mã số thuế (nếu có)
        if (taxIdText != null)
        {
            string displayTaxId = string.IsNullOrEmpty(data.taxId) ? "MST: N/A" : $"MST: {data.taxId}";
            taxIdText.text = displayTaxId;
        }
    }

    private void OnEditButtonClicked()
    {
        // Kích hoạt callback để CustomerManager mở panel chỉnh sửa
        onEditRequested?.Invoke(currentCustomerData);
    }
}