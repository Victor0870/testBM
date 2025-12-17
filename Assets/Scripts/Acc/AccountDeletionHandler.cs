using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class AccountDeletionHandler : MonoBehaviour
{
    [Header("UI References")]
    public Button deleteAccountButton; // Nút "Xóa tài khoản" trong UI
    public GameObject loadingPanel;    // Panel loading để chặn thao tác khi đang xóa

    private FirebaseUser user;
    private FirebaseFirestore db;

    void Start()
    {
        db = FirebaseFirestore.DefaultInstance;
        user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (deleteAccountButton != null)
        {
            deleteAccountButton.onClick.AddListener(OnDeleteAccountClicked);
        }
    }

    private void OnDeleteAccountClicked()
    {
        // 1. Hiển thị Popup cảnh báo xác nhận
        StatusPopupManager.Instance.ShowPopup(
            "CẢNH BÁO: Hành động này sẽ xóa vĩnh viễn tài khoản và toàn bộ dữ liệu cửa hàng của bạn (Khách hàng, Đơn hàng, Kho). Không thể khôi phục.\n\nBạn có chắc chắn muốn tiếp tục?",
            () => 
            {
                // Callback khi bấm OK -> Xác nhận lần 2 cho chắc chắn
                ConfirmDeletionFinal();
            }
        );
    }

    private void ConfirmDeletionFinal()
    {
        StatusPopupManager.Instance.ShowPopup(
            "XÁC NHẬN LẦN CUỐI: Dữ liệu sẽ bị mất vĩnh viễn. Bạn thực sự muốn xóa?",
            () =>
            {
                // Bắt đầu quy trình xóa
                PerformAccountDeletion();
            }
        );
    }

    private async void PerformAccountDeletion()
    {
        if (user == null) return;

        if (loadingPanel != null) loadingPanel.SetActive(true);

        try
        {
            string userId = user.UserId;

            // BƯỚC 1: Xóa dữ liệu trong Firestore
            // Lưu ý: Firestore Client SDK không hỗ trợ xóa đệ quy (xóa cả collection con).
            // Ta sẽ xóa các collection chính và Document Shop.
            
            // Xóa các sub-collections (Cố gắng xóa dữ liệu quan trọng nhất)
            await DeleteCollection(db.Collection("shops").Document(userId).Collection("products"));
            await DeleteCollection(db.Collection("shops").Document(userId).Collection("customers"));
            await DeleteCollection(db.Collection("shops").Document(userId).Collection("sales"));
            
            // Xóa Document chính của Shop
            await db.Collection("shops").Document(userId).DeleteAsync();

            // BƯỚC 2: Xóa tài khoản Authentication (Quan trọng nhất)
            await user.DeleteAsync();

            Debug.Log("Đã xóa tài khoản Firebase thành công.");

            // BƯỚC 3: Xóa dữ liệu Local trên máy
            ShopSessionData.ClearAllData();
            
            // Đặt cờ để AuthManager biết là vừa logout/xóa
            AuthSession.ComingFromLogout = true;

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (loadingPanel != null) loadingPanel.SetActive(false);
                
                StatusPopupManager.Instance.ShowPopup(
                    "Tài khoản của bạn đã được xóa thành công. Ứng dụng sẽ khởi động lại.",
                    () => 
                    {
                        // Chuyển về màn hình Login
                        UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
                    }
                );
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi xóa tài khoản: {e.Message}");
            
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (loadingPanel != null) loadingPanel.SetActive(false);

                // Xử lý lỗi đặc biệt: RequiresRecentLogin
                // Firebase yêu cầu nếu đăng nhập quá lâu thì phải đăng nhập lại mới cho xóa
                if (e.Message.Contains("requires recent authentication") || e.Message.Contains("CREDENTIAL_TOO_OLD_LOGIN_AGAIN"))
                {
                    StatusPopupManager.Instance.ShowPopup(
                        "Bảo mật: Để xóa tài khoản, bạn cần vừa mới đăng nhập.\n\nVui lòng Đăng xuất và Đăng nhập lại, sau đó thử xóa ngay lập tức."
                    );
                }
                else
                {
                    StatusPopupManager.Instance.ShowPopup($"Lỗi khi xóa tài khoản: {e.Message}");
                }
            });
        }
    }

    // Hàm hỗ trợ xóa documents trong 1 collection (Client-side delete)
    // Lưu ý: Chỉ xóa được số lượng nhỏ/vừa. Nếu dữ liệu quá lớn nên dùng Cloud Functions (nhưng ở đây ta làm Client cho đơn giản)
    private async Task DeleteCollection(CollectionReference collectionRef)
    {
        var snapshot = await collectionRef.GetSnapshotAsync();
        var batch = db.StartBatch();
        int count = 0;

        foreach (var doc in snapshot.Documents)
        {
            batch.Delete(doc.Reference);
            count++;
            // Firestore giới hạn batch 500 ops, ta cắt nhỏ nếu cần (ở đây làm đơn giản)
            if (count >= 400) 
            {
                await batch.CommitAsync();
                batch = db.StartBatch();
                count = 0;
            }
        }
        if (count > 0) await batch.CommitAsync();
    }
}