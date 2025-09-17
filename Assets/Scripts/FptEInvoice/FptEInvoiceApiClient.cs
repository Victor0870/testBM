// File: FptEInvoiceApiClient.cs
using UnityEngine;
using UnityEngine.Networking; // Để xử lý HTTP requests
using System.Collections; // Để dùng Coroutine
using System.Text; // Để xử lý string (JSON)
using System;
using System.Threading.Tasks; // Để dùng async/await
using System.Collections.Generic; // Nếu cần
using SimpleJSON; // Đảm bảo bạn đã cài đặt thư viện SimpleJSON và không còn lỗi đỏ
using Firebase.Firestore; // Cần cho Timestamp
using static ShopSessionData; // Truy cập CachedShopSettings, GlobalAnnouncementMessage
using static ShopSettingManager; // Truy cập ShopData class

public class FptEInvoiceApiClient : MonoBehaviour
{
    public static FptEInvoiceApiClient Instance { get; private set; }

    // Giữ lại FptEInvoiceConfig để làm nơi chứa các URL API mặc định (UAT/Production)
    // Người dùng sẽ có thể ghi đè chúng qua Shop Settings nếu cần.
    [Header("Default Configuration (from ScriptableObject)")]
    public FptEInvoiceConfig fptDefaultConfig; // Kéo Asset MyFptEInvoiceConfig vào đây trong Inspector

    // Firebase
    private FirebaseFirestore db;
    private Firebase.Auth.FirebaseUser currentUser; // Để biết user ID nào để cập nhật Firestore
    private DocumentReference shopDocRef; // Tham chiếu đến document shop của user hiện tại

    // Biến trạng thái để kiểm soát việc gọi SignInAsync
    private bool _isSigningIn = false;
    private TaskCompletionSource<bool> _signInCompletionSource;


    void Awake()
    {
        // 1. Đảm bảo chỉ có một instance duy nhất
        if (UnityEngine.Object.FindObjectsByType<FptEInvoiceApiClient>(FindObjectsSortMode.None).Length > 1)
        {
            Debug.LogWarning("FptEInvoiceApiClient: Đã có một instance khác tồn tại. Hủy bản sao mới này.");
            Destroy(this.gameObject); // Hủy bản sao thừa
            return;
        }

        // 2. Thiết lập Singleton Instance
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject); // Giữ lại giữa các Scene
            db = FirebaseFirestore.DefaultInstance; // Khởi tạo Firestore ở đây
            Firebase.Auth.FirebaseAuth.DefaultInstance.StateChanged += AuthStateChanged; // Lắng nghe trạng thái đăng nhập
        }
        else
        {
            // Điều này chỉ nên xảy ra nếu có lỗi logic, nhưng kiểm tra an toàn
            Debug.LogWarning("FptEInvoiceApiClient: Instance đã tồn tại khi Awake được gọi. Tự hủy.");
            Destroy(this.gameObject);
            return;
        }

        // Kiểm tra cấu hình mặc định
        if (fptDefaultConfig == null)
        {
            Debug.LogError("FptEInvoiceApiClient: fptDefaultConfig chưa được gán! Vui lòng kéo Asset FPT eInvoice Config vào Inspector của GameObject này.");
        }
    }

    void OnDestroy()
    {
        if (Firebase.Auth.FirebaseAuth.DefaultInstance != null)
        {
            Firebase.Auth.FirebaseAuth.DefaultInstance.StateChanged -= AuthStateChanged;
        }
    }

    // Lắng nghe trạng thái đăng nhập để cập nhật currentUser và shopDocRef
    private void AuthStateChanged(object sender, EventArgs e)
    {
        Firebase.Auth.FirebaseUser newUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                shopDocRef = db.Collection("shops").Document(currentUser.UserId);
                Debug.Log($"FptEInvoiceApiClient: ShopDocRef set for UID: {currentUser.UserId}");
            }
            else
            {
                shopDocRef = null;
                Debug.Log("FptEInvoiceApiClient: User logged out. ShopDocRef cleared.");
            }
        }
    }


    // Phương thức để gọi API đăng nhập và lấy token
    // Lấy thông tin username, password, signInUrl từ CachedShopSettings
    public async Task<bool> SignInAsync()
    {
        // Kiểm tra xem đã có một tiến trình đăng nhập đang diễn ra chưa
        if (_isSigningIn)
        {
            Debug.Log("FptEInvoiceApiClient: SignInAsync - Đã có một tiến trình đăng nhập đang diễn ra. Đang chờ kết quả...");
            // Đợi kết quả của tiến trình đăng nhập hiện tại
            return await _signInCompletionSource.Task;
        }

        _isSigningIn = true;
        _signInCompletionSource = new TaskCompletionSource<bool>();
        bool signInResult = false; // Mặc định là thất bại

        try
        {
            // Kiểm tra dữ liệu cấu hình đã có trong cache chưa
            if (CachedShopSettings == null || string.IsNullOrEmpty(CachedShopSettings.eInvoiceUser) || string.IsNullOrEmpty(CachedShopSettings.eInvoicePass))
            {
                StatusPopupManager.Instance.ShowPopup("Lỗi cấu hình FPT eInvoice trong Shop Settings. Vui lòng cập nhật.");
                Debug.LogError("FptEInvoiceApiClient: Cannot sign in, missing e-invoice user/pass in CachedShopSettings.");
                signInResult = false;
                return signInResult;
            }
            if (string.IsNullOrEmpty(fptDefaultConfig?.signInUrl))
            {
                StatusPopupManager.Instance.ShowPopup("Lỗi cấu hình API Sign-in URL mặc định. Vui lòng kiểm tra FptEInvoiceConfig.");
                Debug.LogError("FptEInvoiceApiClient: Sign-in URL in default config is empty.");
                signInResult = false;
                return signInResult;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                StatusPopupManager.Instance.ShowPopup("Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
                Debug.LogError("FptEInvoiceApiClient: Cannot sign in, no internet connection.");
                signInResult = false;
                return signInResult;
            }

            string apiUsername = CachedShopSettings.eInvoiceUser;
            string apiPassword = CachedShopSettings.eInvoicePass;
            string signInUrl = fptDefaultConfig.signInUrl; // Lấy URL từ default config

            Debug.Log("FptEInvoiceApiClient: Đang cố gắng lấy access token từ FPT...");
            Debug.Log($"FptEInvoiceApiClient: Đăng nhập với Username API: '{apiUsername}'");

            // Tạo body JSON cho sign-in request
            string jsonBody = $"{{\"username\":\"{apiUsername}\",\"password\":\"{apiPassword}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest webRequest = new UnityWebRequest(signInUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json"); // Quan trọng: Đặt Content-Type là JSON

                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield(); // Đợi cho đến khi request hoàn tất
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    Debug.Log("FptEInvoiceApiClient: Phản hồi Sign-in (RAW): " + responseText);

                    try
                    {
                        // Chia chuỗi JWT thành 3 phần: Header.Payload.Signature
                        string[] jwtParts = responseText.Split('.');

                        if (jwtParts.Length != 3)
                        {
                            StatusPopupManager.Instance.ShowPopup("Lỗi: Phản hồi đăng nhập FPT không phải định dạng JWT hợp lệ.");
                            Debug.LogError("FptEInvoiceApiClient: Phản hồi không phải JWT 3 phần. Phản hồi: " + responseText);
                            signInResult = false;
                        }
                        else
                        {
                            // Giải mã phần Payload (phần tử thứ 2, index 1)
                            string payloadBase64Url = jwtParts[1];
                            string decodedPayloadJson = DecodeBase64Url(payloadBase64Url);

                            Debug.Log("FptEInvoiceApiClient: Decoded JWT Payload: " + decodedPayloadJson);

                            var json = SimpleJSON.JSON.Parse(decodedPayloadJson);

                            // Kiểm tra xem payload đã giải mã có phải là một JSON object không
                            if (json != null && json.IsObject)
                            {
                                // Lấy thời gian hết hạn (exp) từ payload của JWT (Unix timestamp)
                                long expUnixTime = json["exp"].AsLong;

                                if (!string.IsNullOrEmpty(responseText))
                                {
                                    // Cập nhật AccessToken và TokenExpiryTime vào CachedShopSettings
                                    // Đây là dữ liệu động, sẽ được lưu vào Firestore sau
                                    ShopSessionData.CachedShopSettings.fptAccessToken = responseText;
                                    ShopSessionData.CachedShopSettings.fptTokenExpiryTime = expUnixTime;

                                    // Cập nhật vào Firestore và PlayerPrefs
                                    await UpdateFptTokenInFirestoreAndCache(responseText, expUnixTime);

                                    Debug.Log("FptEInvoiceApiClient: Đã lấy và lưu access token thành công.");
                                    signInResult = true;
                                }
                                else
                                {
                                    StatusPopupManager.Instance.ShowPopup("Lỗi: Không nhận được access token hợp lệ từ FPT.");
                                    Debug.LogError("FptEInvoiceApiClient: Access token (toàn bộ JWT) rỗng.");
                                    signInResult = false;
                                }
                            }
                            else
                            {
                                StatusPopupManager.Instance.ShowPopup("Lỗi: Payload JWT không phải JSON object hoặc thiếu thông tin cần thiết.");
                                Debug.LogError($"FptEInvoiceApiClient: Payload JWT không đúng định dạng. Payload: {decodedPayloadJson}");
                                signInResult = false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        StatusPopupManager.Instance.ShowPopup("Lỗi phân tích hoặc giải mã JWT từ FPT: " + e.Message);
                        Debug.LogError("FptEInvoiceApiClient: Lỗi phân tích hoặc giải mã JWT: " + e.Message + " RAW Response: " + responseText);
                        signInResult = false;
                    }
                }
                else
                {
                    string errorDetails = string.IsNullOrEmpty(webRequest.downloadHandler.text) ? webRequest.error : webRequest.downloadHandler.text;
                    string userMessage;

                    if (webRequest.responseCode == 429) // Nếu lỗi là Too Many Requests, đừng xử lý popup ở đây
                    {
                        // Lỗi 429 sẽ được xử lý ở GetValidAccessToken hoặc SendApiRequestAsync
                        Debug.LogWarning($"FptEInvoiceApiClient: SignInAsync - Gặp lỗi 429. Sẽ thử lại ở cấp cao hơn.");
                        signInResult = false;
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    {
                        userMessage = "Lỗi kết nối mạng đến máy chủ FPT. Vui lòng kiểm tra Internet của bạn.";
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                        signInResult = false;
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        if (webRequest.responseCode == 401) // Unauthorized
                        {
                            userMessage = "Thông tin đăng nhập FPT eInvoice không chính xác. Vui lòng kiểm tra lại Username/Mật khẩu API trong Cài đặt Shop.";
                        }
                        else if (webRequest.responseCode >= 500 && webRequest.responseCode < 600) // Server errors
                        {
                            userMessage = "Máy chủ FPT eInvoice đang gặp sự cố. Vui lòng thử lại sau ít phút.";
                        }
                        else // Các lỗi protocol khác (ví dụ 400 Bad Request, 404 Not Found)
                        {
                            // Cố gắng parse body lỗi nếu là JSON chứa thông báo lỗi
                            string fptApiSpecificError = errorDetails;
                            try {
                                var errorJson = SimpleJSON.JSON.Parse(errorDetails);
                                if (errorJson != null && !errorJson.IsNull) {
                                    // Thử các trường lỗi phổ biến trong phản hồi API của FPT eInvoice
                                    // Ví dụ: {"message": "Invalid credentials"}, {"error": "..."}
                                    // Dựa trên tài liệu bạn cung cấp: "Body": "Message chi tiết lỗi" cho lỗi 400
                                    fptApiSpecificError = errorJson.Value;
                                    if (string.IsNullOrEmpty(fptApiSpecificError) && errorJson["message"] != null) fptApiSpecificError = errorJson["message"].Value;
                                    if (string.IsNullOrEmpty(fptApiSpecificError) && errorJson["error"] != null) fptApiSpecificError = errorJson["error"].Value;
                                }
                            } catch { /* Ignore parsing error, use raw errorDetails */ }

                            userMessage = $"Lỗi từ FPT eInvoice (Mã: {webRequest.responseCode}). Chi tiết: {fptApiSpecificError}";
                        }
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                        signInResult = false;
                    }
                    else // DataProcessingError hoặc các loại lỗi khác
                    {
                        userMessage = "Đã xảy ra lỗi không mong muốn khi giao tiếp với FPT eInvoice. Vui lòng thử lại.";
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                        signInResult = false;
                    }
                    Debug.LogError($"FptEInvoiceApiClient: Lỗi Sign-in: {webRequest.error}. Chi tiết: {errorDetails}");
                }
            }
        }
        finally
        {
            // Đặt kết quả cho TaskCompletionSource và reset cờ
            _signInCompletionSource.SetResult(signInResult);
            _isSigningIn = false;
        }
        return signInResult;
    }

    // Phương thức kiểm tra token và tự động refresh nếu cần
    public async Task<string> GetValidAccessToken()
    {
        // Kiểm tra xem đã có dữ liệu shop trong cache chưa
        if (CachedShopSettings == null)
        {
            // Điều này không nên xảy ra nếu AuthManager đã tải đúng
            Debug.LogError("FptEInvoiceApiClient: GetValidAccessToken - CachedShopSettings is null. Cannot validate token.");
            return null;
        }

        string currentAccessToken = CachedShopSettings.fptAccessToken;
        long currentTokenExpiryTime = CachedShopSettings.fptTokenExpiryTime;

        // Kiểm tra xem token còn hợp lệ không (ví dụ: token hợp lệ trong 24h, nhưng ta sẽ làm check an toàn hơn là 23h)
        // DateTimeOffset.UtcNow.ToUnixTimeSeconds() cung cấp thời gian hiện tại dưới dạng Unix timestamp
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // An toàn: token hết hạn 1 tiếng trước thời gian thực sự hết hạn
        bool isTokenStillValid = !string.IsNullOrEmpty(currentAccessToken) && (currentTokenExpiryTime > (currentTime + 3600));

        if (isTokenStillValid)
        {
            return currentAccessToken;
        }

        Debug.Log("FptEInvoiceApiClient: Access token hết hạn hoặc chưa có. Đang refresh token...");

        // Thêm logic thử lại cấp số nhân cho SignInAsync
        int maxRetries = 3;
        int retryDelayMs = 200; // Bắt đầu với 200ms để đảm bảo vượt qua giới hạn 0.05s của FPT

        for (int retryCount = 0; retryCount < maxRetries; retryCount++)
        {
            bool signInSuccess = await SignInAsync(); // Gọi SignInAsync đã được sửa đổi
            if (signInSuccess)
            {
                return CachedShopSettings.fptAccessToken; // Trả về token mới từ cache
            }
            else
            {
                // Nếu SignInAsync báo hiệu lỗi 429 hoặc các lỗi tạm thời khác, ta sẽ thử lại.
                Debug.LogWarning($"FptEInvoiceApiClient: SignInAsync thất bại. Thử lại sau {retryDelayMs}ms... (Lần {retryCount + 1}/{maxRetries})");
                StatusPopupManager.Instance.ShowPopup($"Không thể lấy token FPT. Đang thử lại... Vui lòng chờ.");
                await Task.Delay(retryDelayMs);
                retryDelayMs *= 2; // Tăng gấp đôi thời gian chờ
            }
        }
        Debug.LogError("FptEInvoiceApiClient: Không thể lấy token FPT sau nhiều lần thử lại.");
        StatusPopupManager.Instance.ShowPopup("Không thể kết nối với dịch vụ FPT eInvoice. Vui lòng thử lại sau.");
        return null; // Không thể lấy được token hợp lệ
    }

    // Hàm tiện ích để giải mã Base64Url (đặc biệt cho JWT)
    private string DecodeBase64Url(string base64Url)
    {
        string base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        byte[] data = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(data);
    }

    // Phương thức chung để gửi request API tới FPT eInvoice
    // Sử dụng SimpleJSON.JSONNode để linh hoạt với payload JSON
    public async Task<(bool success, string responseData, string errorMessage)> SendApiRequestAsync(
        string url, string method, JSONNode payload = null)
    {
         // THAY ĐỔI: Thêm kiểm tra kết nối Internet chung
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // Gửi lỗi thân thiện để SalesManager hiển thị popup
                return (false, null, "Không có kết nối Internet. Vui lòng kiểm tra mạng và thử lại.");
            }
        string accessToken = await GetValidAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            return (false, null, "Không thể lấy access token để gọi API FPT.");
        }

        using (UnityWebRequest webRequest = new UnityWebRequest(url, method))
        {
            webRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
            webRequest.SetRequestHeader("Content-Type", "application/json"); // FPT API expects JSON

            if (payload != null)
            {
                string jsonPayload = payload.SaveToJSON(JSONTextMode.Compact); // Sử dụng SaveToJSON của SimpleJSON
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            webRequest.downloadHandler = new DownloadHandlerBuffer();

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"FptEInvoiceApiClient: API call {url} successful.");
                return (true, webRequest.downloadHandler.text, null);
            }
            else
            {
                    string errorDetails = string.IsNullOrEmpty(webRequest.downloadHandler.text) ? webRequest.error : webRequest.downloadHandler.text;
                    string userMessage;

                    if (webRequest.responseCode == 429) // Lỗi Too Many Requests
                    {
                        // Lỗi 429 từ SendApiRequestAsync, có thể do token đã hết hạn ngay sau khi GetValidAccessToken
                        // hoặc do một lý do khác. GetValidAccessToken đã có retry cho SignInAsync.
                        // Tại đây, ta sẽ trả về lỗi để SalesFptInvoiceManager xử lý (có thể retry toàn bộ quy trình)
                        userMessage = "Vượt quá tần suất truy cập FPT. Vui lòng thử lại sau giây lát.";
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                        Debug.LogWarning($"FptEInvoiceApiClient: SendApiRequestAsync - Gặp lỗi 429. Chi tiết: {errorDetails}");
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    {
                        userMessage = "Lỗi kết nối mạng đến máy chủ FPT. Vui lòng kiểm tra Internet của bạn.";
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        if (webRequest.responseCode == 401) // Unauthorized
                        {
                            userMessage = "Thông tin đăng nhập FPT eInvoice không chính xác. Vui lòng kiểm tra lại Username/Mật khẩu API trong Cài đặt Shop.";
                        }
                        else if (webRequest.responseCode >= 500 && webRequest.responseCode < 600) // Server errors
                        {
                            userMessage = "Máy chủ FPT eInvoice đang gặp sự cố. Vui lòng thử lại sau ít phút.";
                        }
                        else // Các lỗi protocol khác (ví dụ 400 Bad Request, 404 Not Found)
                        {
                            // Cố gắng parse body lỗi nếu là JSON chứa thông báo lỗi
                            string fptApiSpecificError = errorDetails;
                            try {
                                var errorJson = SimpleJSON.JSON.Parse(errorDetails);
                                if (errorJson != null && !errorJson.IsNull) {
                                    // Thử các trường lỗi phổ biến trong phản hồi API của FPT eInvoice
                                    // Ví dụ: {"message": "Invalid credentials"}, {"error": "..."}
                                    // Dựa trên tài liệu bạn cung cấp: "Body": "Message chi tiết lỗi" cho lỗi 400
                                    fptApiSpecificError = errorJson.Value;
                                    if (string.IsNullOrEmpty(fptApiSpecificError) && errorJson["message"] != null) fptApiSpecificError = errorJson["message"].Value;
                                    if (string.IsNullOrEmpty(fptApiSpecificError) && errorJson["error"] != null) fptApiSpecificError = errorJson["error"].Value;
                                }
                            } catch { /* Ignore parsing error, use raw errorDetails */ }

                            userMessage = $"Lỗi từ FPT eInvoice (Mã: {webRequest.responseCode}). Chi tiết: {fptApiSpecificError}";
                        }
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                    }
                    else // DataProcessingError hoặc các loại lỗi khác
                    {
                        userMessage = "Đã xảy ra lỗi không mong muốn khi giao tiếp với FPT eInvoice. Vui lòng thử lại.";
                        StatusPopupManager.Instance.ShowPopup(userMessage);
                    }

                    Debug.LogError($"FptEInvoiceApiClient: Lỗi API Call: {webRequest.error}. Chi tiết: {errorDetails}");
                    return (false, null, userMessage);
                }
        }
    }

    // <summary>
    // Cập nhật token và thời gian hết hạn vào Firestore cho shop của người dùng hiện tại và vào cache.
    // </summary>
    // <param name="accessToken">Access token mới.</param>
    // <param name="expiryTime">Thời gian hết hạn của token (Unix timestamp).</param>
    private async Task UpdateFptTokenInFirestoreAndCache(string accessToken, long expiryTime)
    {
        if (shopDocRef == null || currentUser == null)
        {
            Debug.LogError("FptEInvoiceApiClient: Cannot update FPT token in Firestore. ShopDocRef or currentUser is null.");
            return;
        }

        try
        {
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "fptAccessToken", accessToken },
                { "fptTokenExpiryTime", expiryTime }
            };
            await shopDocRef.UpdateAsync(updates);

            // Cập nhật lại ShopSessionData.CachedShopSettings để đồng bộ
            ShopSessionData.CachedShopSettings.fptAccessToken = accessToken;
            ShopSessionData.CachedShopSettings.fptTokenExpiryTime = expiryTime;
            // Gọi lại SetCachedShopSettings để đảm bảo PlayerPrefs cũng được cập nhật
            ShopSessionData.SetCachedShopSettings(currentUser.UserId, ShopSessionData.CachedShopSettings);

            Debug.Log($"FptEInvoiceApiClient: FPT token updated in Firestore and cache for user {currentUser.UserId}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"FptEInvoiceApiClient: Error updating FPT token in Firestore: {e.Message}");
        }
    }
}