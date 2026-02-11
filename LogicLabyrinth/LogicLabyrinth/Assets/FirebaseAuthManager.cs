using Firebase.Auth;
using Firebase.Extensions;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


public class FirebaseAuthManager : MonoBehaviour
{
    public static FirebaseAuthManager Instance;
    private FirebaseAuth auth;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); auth = FirebaseAuth.DefaultInstance; }
        else { Destroy(gameObject); }
    }

    public string GetCurrentUserId() => (auth != null && auth.CurrentUser != null) ? auth.CurrentUser.UserId : "";


    public async void SignInWithGoogle()
    {
        string clientId = "639946562343-skf6qj8519f0gni6tm9frjoac379ueta.apps.googleusercontent.com";
        string clientSecret = "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";
        string redirectUri = "http://localhost:8000/";

        using (var server = new LocalAuthServer(8000))
        {
            server.Start();
            Application.OpenURL($"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=openid%20email%20profile");
            string code = await server.WaitForAuthCode(TimeSpan.FromMinutes(5));
            if (!string.IsNullOrEmpty(code)) await ExchangeCode(code, clientId, clientSecret, redirectUri);
        }
    }

    private async Task ExchangeCode(string code, string clientId, string clientSecret, string redirectUri)
    {
        WWWForm form = new WWWForm();
        form.AddField("code", code);
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("redirect_uri", redirectUri);
        form.AddField("grant_type", "authorization_code");

        using (UnityWebRequest www = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
        {
            await www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                var data = JsonUtility.FromJson<GoogleTokenResponse>(www.downloadHandler.text);
                // FIX: Define credential variable
                Credential credential = GoogleAuthProvider.GetCredential(data.id_token, null);
                auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
                    if (task.IsCompletedSuccessfully)
                    {
                        AccountManager.Instance.LinkWithGoogle(task.Result.Email, task.Result.UserId, task.Result.DisplayName);
                    }
                    else
                    {
                        Debug.LogError("Google Token Exchange Failed: " + www.error);
                        Debug.LogError("Response: " + www.downloadHandler.text);
                    }
                });
            }
        }
    }
    [Serializable] public class GoogleTokenResponse { public string id_token; }
}