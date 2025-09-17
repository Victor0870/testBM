using UnityEngine;
using Firebase;
using Firebase.Extensions;

public class FirebaseInitializer : MonoBehaviour
{
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            if (task.Result == DependencyStatus.Available)
                Debug.Log("🔥 Firebase đã sẵn sàng!");
            else
                Debug.LogError("❌ Firebase chưa hoạt động: " + task.Result);
        });
    }
}
