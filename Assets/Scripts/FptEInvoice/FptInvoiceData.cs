// File: FptInvoiceData.cs
using System;
using System.Collections.Generic;
using SimpleJSON; // Sử dụng SimpleJSON

// Lớp chính đại diện cho cấu trúc hóa đơn tổng thể theo API FPT
public class FptInvoiceData
{
    public string lang { get; set; } // Ngôn ngữ phản hồi, ví dụ "vi" hoặc "en" 

    public InvoiceBody inv { get; set; } // Chứa tất cả thông tin chi tiết hóa đơn

    public FptInvoiceData()
    {
        // Constructor mặc định
        lang = "vi"; // Mặc định tiếng Việt
        inv = new InvoiceBody();
    }

    // Phương thức chuyển đổi đối tượng này thành JSONNode
    public JSONNode ToJsonNode()
    {
        JSONObject root = new JSONObject();
        root["lang"] = lang;
        root["inv"] = inv.ToJsonNode();
        return root;
    }
}

// Lớp chứa thông tin chi tiết của hóa đơn (tức là cụm thẻ "inv" trong JSON)
public class InvoiceBody
{
    // Thông tin chung hóa đơn (Mục 3.1)
    public string type { get; set; } // Loại hóa đơn: 01/MTT, 02/MTT, v.v. 
    public string form { get; set; } // Mẫu số ký hiệu hóa đơn 
    public string serial { get; set; } // Ký hiệu hóa đơn 
    public string seq { get; set; } // Số hóa đơn 
    public string ma_cqthu { get; set; } // Mã cơ quan thuế cấp 
    public string idt { get; set; } // Ngày hóa đơn (yyyy-mm-dd hh:mm:ss) 
    public string sid { get; set; } // Key xác định tính duy nhất của giao dịch payload 
    public string sec { get; set; } // Mã tra cứu (mã bí mật) 
    public string paym { get; set; } // Hình thức thanh toán (TM, CK, TM/CK, v.v.) 
    public string note { get; set; } // Ghi chú hóa đơn 

    // Các thẻ điều hướng (Mục 3.2)
    public int? aun { get; set; } // Xác định phương thức cấp số & mã CQT (1, 2, 3) 
    public int? @class { get; set; } // Nghiệp vụ được thực hiện (0, 2, 4) 
    public int? type_ref { get; set; } // Loại hóa đơn áp dụng (1: NĐ123) 
    public int? sendfile { get; set; } // Xác định đính kèm file (1: có) 
    public int? notsendmail { get; set; } // Không gửi email (1: không gửi) 
    public int? inv_paper { get; set; } // Dùng cho Thay thế/Điều chỉnh HĐ ngoài HT (1: có)

    // Thông tin người bán (Mục 3.3)
    public string stax { get; set; } // Mã số thuế người bán

    // Thông tin người mua (Mục 3.4)
    public string bcode { get; set; } // Mã khách hàng
    public string bname { get; set; } // Tên khách hàng
    public string btax { get; set; } // Mã số thuế khách hàng
    public string baddr { get; set; } // Địa chỉ khách hàng
    public string buyer { get; set; } // Họ và tên cá nhân người mua hàng
    public string budget_relationid { get; set; } // Mã quan hệ ngân sách
    public string idnumber { get; set; } // Căn cước công dân
    public string passport_number { get; set; } // Số hộ chiếu
    public string btel { get; set; } // Số điện thoại người mua hàng
    public string bmail { get; set; } // Địa chỉ email nhận hóa đơn

    // Thông tin hóa đơn liên quan (Mục 3.5 - dùng cho điều chỉnh/thay thế)
    public RelatedInvoiceInfo adj { get; set; } // Cụm thẻ inv.adj

    // Thông tin đặc biệt, dùng cho hóa đơn xăng dầu (Mục 3.6)
    public string p_dt { get; set; } // Thời gian bán hàng xăng dầu
    public string p_pr { get; set; } // Đơn giá đã có VAT (xăng dầu)
    public string p_qty { get; set; } // Số lượng (số lít) (xăng dầu)
    public string p_nozzle { get; set; } // Mã vòi bơm
    public string p_station { get; set; } // Mã cửa hàng
    public string p_code { get; set; } // Mã hàng (xăng dầu)

    // Thông tin hàng hóa, dịch vụ (Mục 3.7)
    public List<FptInvoiceItem> items { get; set; } // Danh sách các sản phẩm trong hóa đơn

    // THÔNG TIN THANH TOÁN (Mục 3.8) - ĐÃ DI CHUYỂN TỪ PaymentSummary LÊN TRỰC TIẾP INV
    public double sum { get; set; } // Tổng tiền trước thuế (nguyên tệ) - BẮT BUỘC
    public double vat { get; set; } // Tổng tiền thuế (nguyên tệ) - BẮT BUỘC
    public double total { get; set; } // Tổng tiền thanh toán sau thuế (nguyên tệ) - BẮT BUỘC
    public double sumv { get; set; } // Tổng tiền chưa thuế (quy đổi sang VNĐ) - BẮT BUỘC
    public double vatv { get; set; } // Tổng tiền thuế (quy đổi sang VNĐ) - BẮT BUỘC
    public double totalv { get; set; } // Tổng tiền thanh toán sau thuế (quy đổi sang VNĐ) - BẮT BUỘC
    public double tradeamount { get; set; } // Tổng tiền chiết khấu thương mại (Tùy chọn)
    public double discount { get; set; } // Tổng giảm trừ khác (Tùy chọn)
    public string word { get; set; } // Số tiền thanh toán bằng chữ (Tùy chọn)


    public InvoiceBody()
    {
        // Khởi tạo các list và đối tượng con để tránh null reference
        items = new List<FptInvoiceItem>();
    }

    // Phương thức chuyển đổi đối tượng này thành JSONObject
    public JSONObject ToJsonNode()
    {
        JSONObject invJson = new JSONObject();

        // Thông tin chung hóa đơn
        if (type != null) invJson["type"] = type;
        if (form != null) invJson["form"] = form;
        if (serial != null) invJson["serial"] = serial;
        if (seq != null) invJson["seq"] = seq;
        if (ma_cqthu != null) invJson["ma_cqthu"] = ma_cqthu;
        if (idt != null) invJson["idt"] = idt;
        if (sid != null) invJson["sid"] = sid;
        if (sec != null) invJson["sec"] = sec;
        if (paym != null) invJson["paym"] = paym;
        if (note != null) invJson["note"] = note;

        // Các thẻ điều hướng
        if (aun.HasValue) invJson["aun"] = aun.Value;
        if (@class.HasValue) invJson["class"] = @class.Value;
        if (type_ref.HasValue) invJson["type_ref"] = type_ref.Value;
        if (sendfile.HasValue) invJson["sendfile"] = sendfile.Value;
        if (notsendmail.HasValue) invJson["notsendmail"] = notsendmail.Value;
        if (inv_paper.HasValue) invJson["inv_paper"] = inv_paper.Value;

        // Thông tin người bán
        if (stax != null) invJson["stax"] = stax;

        // Thông tin người mua
        if (bcode != null) invJson["bcode"] = bcode;
        if (bname != null) invJson["bname"] = bname;
        if (btax != null) invJson["btax"] = btax;
        if (baddr != null) invJson["baddr"] = baddr;
        if (buyer != null) invJson["buyer"] = buyer;
        if (budget_relationid != null) invJson["budget_relationid"] = budget_relationid;
        if (idnumber != null) invJson["idnumber"] = idnumber;
        if (passport_number != null) invJson["passport_number"] = passport_number;
        if (btel != null) invJson["btel"] = btel;
        if (bmail != null) invJson["bmail"] = bmail;

        // Thông tin hóa đơn liên quan (chỉ thêm nếu có dữ liệu)
        if (adj != null && adj.HasData())
        {
            invJson["adj"] = adj.ToJsonNode();
        }

        // Thông tin đặc biệt xăng dầu (chỉ thêm nếu có dữ liệu)
        if (p_dt != null || p_pr != null || p_qty != null || p_nozzle != null || p_station != null || p_code != null)
        {
            if (p_dt != null) invJson["p_dt"] = p_dt;
            if (p_pr != null) invJson["p_pr"] = p_pr;
            if (p_qty != null) invJson["p_qty"] = p_qty;
            if (p_nozzle != null) invJson["p_nozzle"] = p_nozzle;
            if (p_station != null) invJson["p_station"] = p_station;
            if (p_code != null) invJson["p_code"] = p_code;
        }

        // Thông tin hàng hóa, dịch vụ
        if (items != null && items.Count > 0)
        {
            JSONArray itemsArray = new JSONArray();
            foreach (var item in items)
            {
                itemsArray.Add(item.ToJsonNode());
            }
            invJson["items"] = itemsArray;
        }

        // THÔNG TIN THANH TOÁN - LÀ BẮT BUỘC VÀ NẰM TRỰC TIẾP TRONG INV
        // Luôn thêm vào JSON, bất kể giá trị là 0 hay không, vì chúng là BẮT BUỘC.
        invJson["sum"] = sum;
        invJson["vat"] = vat;
        invJson["total"] = total;
        invJson["sumv"] = sumv;
        invJson["vatv"] = vatv;
        invJson["totalv"] = totalv;
        // Các trường tùy chọn khác
        if (tradeamount != 0) invJson["tradeamount"] = tradeamount;
        if (discount != 0) invJson["discount"] = discount;
        if (word != null) invJson["word"] = word;

        return invJson;
    }
}

// Lớp chứa thông tin hóa đơn liên quan (inv.adj)
public class RelatedInvoiceInfo
{
    public string @ref { get; set; } // Văn bản thỏa thuận điều chỉnh/thay thế
    public string rdt { get; set; } // Ngày văn bản thỏa thuận (date)
    public string seq { get; set; } // Số hóa đơn gốc cần điều chỉnh/thay thế
    public string idt { get; set; } // Ngày của hóa đơn gốc
    public string rea { get; set; } // Lý do thực hiện điều chỉnh/thay thế

    // Kiểm tra xem đối tượng này có dữ liệu để chuyển thành JSON không
    public bool HasData()
    {
        return !string.IsNullOrEmpty(@ref) || !string.IsNullOrEmpty(rdt) ||
               !string.IsNullOrEmpty(seq) || !string.IsNullOrEmpty(idt) ||
               !string.IsNullOrEmpty(rea);
    }

    public JSONObject ToJsonNode()
    {
        JSONObject adjJson = new JSONObject();
        if (@ref != null) adjJson["ref"] = @ref;
        if (rdt != null) adjJson["rdt"] = rdt;
        if (seq != null) adjJson["seq"] = seq;
        if (idt != null) adjJson["idt"] = idt;
        if (rea != null) adjJson["rea"] = rea;
        return adjJson;
    }
}