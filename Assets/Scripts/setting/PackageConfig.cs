// File: PackageConfig.cs

using UnityEngine;
using System.Collections.Generic;

// Cho phép tạo Asset PackageConfig từ Unity Editor (Assets/Create/Bizmate/Package Configuration)
[CreateAssetMenu(fileName = "PackageConfig", menuName = "Bizmate/Package Configuration", order = 1)]
public class PackageConfig : ScriptableObject
{
    // Class lồng ghép để định nghĩa chi tiết từng gói trong ScriptableObject
    [System.Serializable]
    public class PackageDetails
    {
        public string packageName; // Tên gói: "Basic", "Advanced", "Pro"
        public List<AppFeature> includedFeatures; // Danh sách các tính năng đi kèm gói (sử dụng Enum AppFeature)
        public long cost; // Chi phí của gói (để tham chiếu trong Editor, có thể đồng bộ với Firestore sau)
        public long defaultDurationDays; // Số ngày gia hạn mặc định (để tham chiếu trong Editor, có thể đồng bộ với Firestore sau)
    }

    // Danh sách các gói dịch vụ có trong ứng dụng
    public List<PackageDetails> packages;

    // Hàm tiện ích để kiểm tra xem một gói có bao gồm một tính năng cụ thể hay không
    public bool HasFeature(string currentPackageName, AppFeature feature)
    {
        if (string.IsNullOrEmpty(currentPackageName) || packages == null) return false;

        // Tìm gói tương ứng theo tên
        PackageDetails package = packages.Find(p => p.packageName == currentPackageName);

        // Kiểm tra nếu gói tồn tại và danh sách tính năng không rỗng
        if (package != null && package.includedFeatures != null)
        {
            // Trả về true nếu gói bao gồm tính năng đó
            return package.includedFeatures.Contains(feature);
        }
        return false; // Gói không tồn tại hoặc không có tính năng nào
    }

    // Hàm tiện ích để lấy chi tiết của một gói theo tên
    public PackageDetails GetPackageDetails(string packageName)
    {
        if (packages == null) return null;
        return packages.Find(p => p.packageName == packageName);
    }
}