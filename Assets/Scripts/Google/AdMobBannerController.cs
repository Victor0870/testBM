using UnityEngine;
using GoogleMobileAds.Api;

public class AdMobBannerController : MonoBehaviour
{
    private BannerView bannerView;

    // ID quảng cáo thử nghiệm của bạn (Banner Test)
#if UNITY_ANDROID
    private string _adUnitId = "ca-app-pub-3940256099942544/6300978111"; // Banner Test Android
#elif UNITY_IPHONE
    private string _adUnitId = "ca-app-pub-3940256099942544/2934735716"; // Banner Test iOS
#else
    private string _adUnitId = "unexpected_platform";
#endif

    void Start()
    {
        // Khoi tao SDK truoc
        MobileAds.Initialize(initStatus => {
            Debug.Log("Google Mobile Ads SDK Initialized.");
            RequestBannerAd();
        });
    }

    public void RequestBannerAd()
    {
        Debug.Log("AdMobBannerController: Dang yeu cau quang cao banner...");

        // Huy banner cu neu co
        if (bannerView != null)
        {
            bannerView.Destroy();
        }

        // Kich thuoc banner - co the dung AdSize.Banner hoac Adaptive
        AdSize adSize = AdSize.Banner;

        bannerView = new BannerView(_adUnitId, adSize, AdPosition.Bottom);

        // Tao AdRequest
        AdRequest request = new AdRequest();

        // Load banner
        bannerView.LoadAd(request);

        Debug.Log("AdMobBannerController: Lenh tai banner da duoc gui.");
    }

    public void DestroyBannerAd()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
            Debug.Log("AdMobBannerController: Quang cao banner da bi huy.");
        }
    }

    void OnDestroy()
    {
        DestroyBannerAd();
    }
}
