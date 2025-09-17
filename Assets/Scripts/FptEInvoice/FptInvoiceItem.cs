// File: FptInvoiceItem.cs
using System;
using SimpleJSON; // Sử dụng SimpleJSON

// Lớp đại diện cho một mục hàng hóa/dịch vụ trong hóa đơn (cụm inv.items)
public class FptInvoiceItem
{
    public int line { get; set; } // Số thứ tự của dòng hàng hóa 
    public string type { get; set; } // Hình thức hàng hóa (CK, KM, MT, 1, 2, 3) 
    public string trip_to { get; set; } // Hành trình - điểm đi (dùng cho dịch vụ vận chuyển) 
    public string trip_from { get; set; } // Hành trình - điểm đến (dùng cho dịch vụ vận chuyển) 
    public string plate_no { get; set; } // Biển kiểm soát (dùng cho dịch vụ vận chuyển) 
    public string frame_no { get; set; } // Số khung (dùng cho hóa đơn bán xe) 
    public string machine_no { get; set; } // Số máy (dùng cho hóa đơn bán xe) 
    public string send_name { get; set; } // Tên người gửi hàng (dùng cho vận chuyển TMĐT) 
    public string send_addr { get; set; } // Địa chỉ - người gửi hàng (dùng cho vận chuyển TMĐT) 
    public string send_tax { get; set; } // MST - người gửi hàng (dùng cho vận chuyển TMĐT) 
    public string send_idno { get; set; } // Số định danh – người gửi hàng (dùng cho vận chuyển TMĐT) 
    public string code { get; set; } // Mã hàng hóa, dịch vụ 
    public string name { get; set; } // Mô tả hàng hóa, dịch vụ 
    public string unit { get; set; } // Đơn vị tính 
    public double price { get; set; } // Đơn giá (chưa thuế) 
    public double quantity { get; set; } // Số lượng 
    public string vrt { get; set; } // Loại thuế suất áp dụng (0, 5, 8, 10, -1, -2) 
    public double perdiscount { get; set; } // Tỷ lệ % chiết khấu 
    public double amtdiscount { get; set; } // Số tiền chiết khấu 
    public double amount { get; set; } // Thành tiền từng dòng hàng hóa dịch vụ 
    public double vat { get; set; } // Số tiền VAT từng hàng hóa 
    public double total { get; set; } // Tổng tiền bao gồm VAT từng hàng hóa 
    public double pricev { get; set; } // Thẻ hỗ trợ quy đổi sang VNĐ (giá) 
    public double amountv { get; set; } // Thẻ hỗ trợ quy đổi sang VNĐ (thành tiền) 
    public double vatv { get; set; } // Thẻ hỗ trợ quy đổi sang VNĐ (vat) 
    public double totalv { get; set; } // Thẻ hỗ trợ quy đổi sang VNĐ (tổng tiền) 

    public FptInvoiceItem()
    {
        // Constructor mặc định
    }

    // Phương thức chuyển đổi đối tượng này thành JSONObject
    public JSONObject ToJsonNode()
    {
        JSONObject itemJson = new JSONObject();
        if (line != 0) itemJson["line"] = line; // line là bắt buộc nếu tự cấp số thứ tự
        if (type != null) itemJson["type"] = type;
        if (trip_to != null) itemJson["trip_to"] = trip_to;
        if (trip_from != null) itemJson["trip_from"] = trip_from;
        if (plate_no != null) itemJson["plate_no"] = plate_no;
        if (frame_no != null) itemJson["frame_no"] = frame_no;
        if (machine_no != null) itemJson["machine_no"] = machine_no;
        if (send_name != null) itemJson["send_name"] = send_name;
        if (send_addr != null) itemJson["send_addr"] = send_addr;
        if (send_tax != null) itemJson["send_tax"] = send_tax;
        if (send_idno != null) itemJson["send_idno"] = send_idno;
        if (code != null) itemJson["code"] = code;
        if (name != null) itemJson["name"] = name;
        if (unit != null) itemJson["unit"] = unit;

        // Chỉ thêm vào JSON nếu có giá trị (không phải 0 mặc định)
        if (price != 0) itemJson["price"] = price;
        if (quantity != 0) itemJson["quantity"] = quantity;
        if (vrt != null) itemJson["vrt"] = vrt;
        if (perdiscount != 0) itemJson["perdiscount"] = perdiscount;
        if (amtdiscount != 0) itemJson["amtdiscount"] = amtdiscount;
        if (amount != 0) itemJson["amount"] = amount;
        if (vat != 0) itemJson["vat"] = vat;
        if (total != 0) itemJson["total"] = total;
        if (pricev != 0) itemJson["pricev"] = pricev;
        if (amountv != 0) itemJson["amountv"] = amountv;
        if (vatv != 0) itemJson["vatv"] = vatv;
        if (totalv != 0) itemJson["totalv"] = totalv;

        return itemJson;
    }
}