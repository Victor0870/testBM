// File: Scripts/TK/TKAuthManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;

public class TKAuthManager : MonoBehaviour
{
    // Tham chiếu trong Inspector
    [Header("Dependencies")]
    public UIDocument uiDocument;
    public TKAuthLogic authLogic; // Kéo component TKAuthLogic.cs vào đây
    public TKStatusPopupManager statusPopupManager;

    // UI Elements
    private VisualElement loginPanel;
    private VisualElement createAccountPanel;
    private TextField loginEmailInput;
    private TextField loginPasswordInput;
    private Button loginButton;
    private Button showCreateAccountButton;

    private TextField signupUsernameInput;
    private TextField signupEmailInput;
    private TextField signupPasswordInput;
    private TextField signupConfirmPasswordInput;
    private Button createAccountButton;
    private Button showLoginButton;
    private Button forgotPasswordButton;

    // Status
    private Label globalStatusLabel;

    async void Start()
    {
        if (uiDocument == null || authLogic == null || statusPopupManager == null)
        {
             Debug.LogError("TKAuthManager: Dependencies (UIDocument/TKAuthLogic/TKStatusPopupManager) is missing.");
             return;
        }

        // Chờ Logic Core khởi tạo xong Firebase
        await authLogic.InitializeFirebaseAsync();

        // 1. Ánh xạ các phần tử UI bằng UQuery (Q<T>("tên ID trong UXML"))
        VisualElement root = uiDocument.rootVisualElement;

        loginPanel = root.Q<VisualElement>("LoginPanelRoot");
        createAccountPanel = root.Q<VisualElement>("CreateAccountPanelRoot");
        globalStatusLabel = root.Q<Label>("global-status-label");

        // Login
        loginEmailInput = root.Q<TextField>("login-email-input");
        loginPasswordInput = root.Q<TextField>("login-password-input");
        loginButton = root.Q<Button>("login-button");
        showCreateAccountButton = root.Q<Button>("show-create-account-button");
        forgotPasswordButton = root.Q<Button>("forgot-password-button");

        // Sign Up
        signupUsernameInput = root.Q<TextField>("signup-username-input");
        signupEmailInput = root.Q<TextField>("signup-email-input");
        signupPasswordInput = root.Q<TextField>("signup-password-input");
        signupConfirmPasswordInput = root.Q<TextField>("signup-confirm-password-input");
        createAccountButton = root.Q<Button>("create-account-button");
        showLoginButton = root.Q<Button>("show-login-button");

        // 2. Gán Listener
        if (loginButton != null) loginButton.clicked += OnLoginButtonClicked;
        if (createAccountButton != null) createAccountButton.clicked += OnCreateAccountButtonClicked;
        if (showCreateAccountButton != null) showCreateAccountButton.clicked += () => TogglePanel(false);
        if (showLoginButton != null) showLoginButton.clicked += () => TogglePanel(true);
        if (forgotPasswordButton != null) forgotPasswordButton.clicked += OnForgotPasswordClicked;

        // Bắt đầu bằng việc hiển thị Login Panel
        TogglePanel(true);
    }

    private void TogglePanel(bool showLogin)
    {
        if (loginPanel != null) loginPanel.style.display = showLogin ? DisplayStyle.Flex : DisplayStyle.None;
        if (createAccountPanel != null) createAccountPanel.style.display = showLogin ? DisplayStyle.None : DisplayStyle.Flex;
        if (globalStatusLabel != null) globalStatusLabel.text = "";
        loginPasswordInput.value = "";
        signupPasswordInput.value = "";
        signupConfirmPasswordInput.value = "";
    }

    private void OnLoginButtonClicked()
    {
        string email = loginEmailInput.value.Trim();
        string password = loginPasswordInput.value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusPopupManager.ShowPopup("Vui lòng điền đầy đủ Email và Mật khẩu để đăng nhập.");
            return;
        }

        if (globalStatusLabel != null) globalStatusLabel.text = "Đang đăng nhập...";
        SetButtonsInteractable(false);

        authLogic.LoginUserWithCredentials(email, password, OnLoginResult);
    }

    private void OnCreateAccountButtonClicked()
    {
        string username = signupUsernameInput.value.Trim();
        string email = signupEmailInput.value.Trim();
        string password = signupPasswordInput.value;
        string confirmPassword = signupConfirmPasswordInput.value;

        // Validation logic
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            statusPopupManager.ShowPopup("Vui lòng điền đầy đủ Tên tài khoản, Email, Mật khẩu và Xác nhận mật khẩu.");
            return;
        }
        if (password.Length < 6)
        {
            statusPopupManager.ShowPopup("Mật khẩu phải có ít nhất 6 ký tự.");
            return;
        }
        if (!authLogic.IsValidEmail(email))
        {
            statusPopupManager.ShowPopup("Địa chỉ email không hợp lệ. Vui lòng kiểm tra lại.");
            return;
        }
        if (password != confirmPassword)
        {
            statusPopupManager.ShowPopup("Mật khẩu xác nhận không khớp. Vui lòng kiểm tra lại.");
            return;
        }

        if (globalStatusLabel != null) globalStatusLabel.text = "Đang tạo tài khoản...";
        SetButtonsInteractable(false);

        authLogic.RegisterUserWithCredentials(email, password, username, OnLoginResult);
    }

    private void OnForgotPasswordClicked()
    {
         statusPopupManager.ShowPopup("Tính năng khôi phục mật khẩu đang được phát triển. Vui lòng liên hệ quản trị viên.");
    }

    private void OnLoginResult(bool success, string statusMessage)
    {
        if (globalStatusLabel != null) globalStatusLabel.text = statusMessage;
        SetButtonsInteractable(true);

        if (!success)
        {
            statusPopupManager.ShowPopup(statusMessage);
        }
        else if (statusMessage.Contains("Tạo tài khoản thành công"))
        {
            statusPopupManager.ShowPopup(statusMessage);
            TogglePanel(true);
        }
        // Nếu thành công và chuyển Scene, statusPopupManager sẽ tự chuyển sang Scene mới.
    }

    private void SetButtonsInteractable(bool interactable)
    {
        loginButton.SetEnabled(interactable);
        createAccountButton.SetEnabled(interactable);
        showCreateAccountButton.SetEnabled(interactable);
        showLoginButton.SetEnabled(interactable);
        forgotPasswordButton.SetEnabled(interactable);
    }
}