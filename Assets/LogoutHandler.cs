using UnityEngine;
using Firebase.Auth; // Cần thiết để truy cập FirebaseAuth
using UnityEngine.SceneManagement; // Để chuyển Scene sau khi đăng xuất

// Nếu bạn đang sử dụng ShopSessionData để quản lý cache cục bộ
using static ShopSessionData;

public class LogoutHandler : MonoBehaviour
{
    // Phương thức này có thể được gọi bởi một nút UI hoặc từ code
    public void SignOutUser()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            Debug.Log("Đang đăng xuất người dùng khỏi Firebase.");
            FirebaseAuth.DefaultInstance.SignOut();

            // Xóa tất cả dữ liệu cache cục bộ và PlayerPrefs
            ShopSessionData.ClearAllData();

            Debug.Log("Đã đăng xuất và xóa dữ liệu cục bộ. Chuyển về màn hình đăng nhập.");
            // Chuyển người dùng về màn hình đăng nhập
            // Đảm bảo "Login" là tên Scene đăng nhập của bạn
            SceneManager.LoadScene("Login");
        }
        else
        {
            Debug.Log("Không có người dùng nào đang đăng nhập.");
            // Đảm bảo xóa dữ liệu cục bộ ngay cả khi không có phiên Firebase active
            ShopSessionData.ClearAllData();
            SceneManager.LoadScene("Login");
        }
    }
}