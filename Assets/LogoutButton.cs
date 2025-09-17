using UnityEngine;

public class LogoutButton : MonoBehaviour
{
    public void OnLogoutClicked()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.SignOutAndReturnToLogin();
        }
        else
        {
            Debug.LogWarning("AuthManager.Instance chưa tồn tại!");
        }
    }
}
