using UnityEngine;
using GoogleMobileAds.Api;
using System; // Cần thêm using này để sử dụng Exception

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    private BannerView bannerView;

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
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Khoi tao Mobile Ads mot lan duy nhat
            MobileAds.Initialize(initStatus =>
            {
                Debug.Log("Google Mobile Ads SDK Initialized (once).");
                // Khi khoi tao xong co the load banner luon
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
        // Xoa banner cu neu co
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        try
        {
            // Lay kich thuoc adaptive banner theo chieu ngang man hinh
            AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                AdSize.FullWidth);

            bannerView = new BannerView(adUnitId, adaptiveSize, AdPosition.Bottom);

            // Tạo request
            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);

            Debug.Log("Adaptive banner requested.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AdMobManager: Adaptive banner not supported, falling back to fixed banner. Error: " + ex.Message);
            // Fallback: gọi phương thức tạo banner cố định
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

        // Banner cố định 320x50
        AdSize adSize = AdSize.Banner;
        bannerView = new BannerView(adUnitId, adSize, AdPosition.Bottom);

        AdRequest adRequest = new AdRequest();
        bannerView.LoadAd(adRequest);

        Debug.Log("AdMobManager: Fixed banner load command sent.");
    }

    // Ham an banner
    public void HideBanner()
    {
        if (bannerView != null)
        {
            bannerView.Hide();
        }
    }

    // Ham hien banner
    public void ShowBanner()
    {
        if (bannerView != null)
        {
            bannerView.Show();
        }
    }

    // Ham huy banner
    public void DestroyBanner()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }
    }
}