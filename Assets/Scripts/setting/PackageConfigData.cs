// File: PackageConfigData.cs

using Firebase.Firestore;
using System.Collections.Generic;

// Class đại diện cho thông tin chi tiết của một gói (Basic, Advanced, Pro) trong Firestore
[FirestoreData]
public class PackageDetailsFirebase
{
    [FirestoreProperty("cost")]
    public long Cost { get; set; } // Chi phí của gói (sử dụng long cho tiền tệ)

    [FirestoreProperty("defaultDurationDays")]
    public long DefaultDurationDays { get; set; } // Số ngày gia hạn mặc định

    [FirestoreProperty("features")]
    public List<string> Features { get; set; } // Danh sách tên tính năng (chuỗi)

    public PackageDetailsFirebase() { } // Constructor mặc định cần thiết cho Firestore
}

// Class đại diện cho toàn bộ cấu trúc của document 'package_costs' trong collection 'app_settings'
[FirestoreData]
public class GlobalAppConfigData
{
    [FirestoreProperty("freeTrialDurationDays")]
    public long FreeTrialDurationDays { get; set; } // Số ngày dùng thử miễn phí

    [FirestoreProperty("Basic")]
    public PackageDetailsFirebase Basic { get; set; } // Thông tin gói Basic

    [FirestoreProperty("Advanced")]
    public PackageDetailsFirebase Advanced { get; set; } // Thông tin gói Advanced

    [FirestoreProperty("Pro")]
    public PackageDetailsFirebase Pro { get; set; } // Thông tin gói Pro

    public GlobalAppConfigData() { } // Constructor mặc định cần thiết cho Firestore
}