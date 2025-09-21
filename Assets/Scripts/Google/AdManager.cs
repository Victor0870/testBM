// AdManager.cs

using UnityEngine;
using GoogleMobileAds.Api;
using System;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    private BannerView bannerView;

    // Thêm một Action để thông báo khi banner được tải thành công
    // float: chiều cao của banner đã tải (đơn vị pixel)
    public static event Action<float> OnBannerLoaded;

    // Id banner
#if UNITY_ANDROID
    private string adUnitId = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IPHONE
    private string adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
    private string adUnitId = "unused";
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            MobileAds.Initialize(initStatus =>
            {
                Debug.Log("Google Mobile Ads SDK Initialized (once).");
                RequestAdaptiveBanner();
            });
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Yeu cau banner adaptive (voi fallback)
    public void RequestAdaptiveBanner()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        try
        {
            AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            bannerView = new BannerView(adUnitId, adaptiveSize, AdPosition.Bottom);

            // Đăng ký sự kiện khi banner được tải thành công
            bannerView.OnBannerAdLoaded += () =>
            {
                Debug.Log("Adaptive banner loaded successfully.");
                // Lấy chiều cao của banner đã tải và gửi đi qua event
                // Sửa: Dùng thuộc tính Height thay cho HeightInPixels
                float bannerHeightInDips = adaptiveSize.Height;
                if (OnBannerLoaded != null)
                {
                    OnBannerLoaded.Invoke(bannerHeightInDips);
                }
            };

            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
            Debug.Log("Adaptive banner requested.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AdMobManager: Adaptive banner not supported, falling back to fixed banner. Error: " + ex.Message);
            RequestFixedBanner();
        }
    }

    // Yêu cầu banner cố định
    public void RequestFixedBanner()
    {
        Debug.Log("AdMobManager: Requesting fixed banner...");

        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        AdSize adSize = AdSize.Banner;
        bannerView = new BannerView(adUnitId, adSize, AdPosition.Bottom);

        // Đăng ký sự kiện khi banner được tải thành công
        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("Fixed banner loaded successfully.");
            // Lấy chiều cao của banner đã tải và gửi đi qua event
            // Sửa: Dùng thuộc tính Height thay cho HeightInPixels
            float bannerHeightInDips = adSize.Height;
            if (OnBannerLoaded != null)
            {
                OnBannerLoaded.Invoke(bannerHeightInDips);
            }
        };

        AdRequest adRequest = new AdRequest();
        bannerView.LoadAd(adRequest);
        Debug.Log("AdMobManager: Fixed banner load command sent.");
    }

    // Các hàm khác không thay đổi...
    public void HideBanner()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
        }
    }

    public void ShowBanner()
    {
        if (bannerView != null)
        {
            bannerView.Show();
        }
    }

    public void DestroyBanner()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }
    }
}