using UnityEngine;
using System;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Extensions;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance;

    [Serializable]
    public class PlayerData
    {
        public string username;
        public string passwordHash;
        public string securityQuestion;
        public string securityAnswer;
        public string googleId;
        public string googleEmail;
        public string displayName;

        public int unlockedLevels = 1;
        public int lastCompletedLevel = 0;

        public List<string> completedPuzzles = new List<string>();
        public List<string> destroyedGates = new List<string>();
        public List<string> collectedGates = new List<string>();
        public int andGatesCollected = 0;
        public int orGatesCollected = 0;
        public int notGatesCollected = 0;
        

        public PlayerData(string user, string pass)
        {
            this.username = user;
            this.passwordHash = pass;
        }
    }

    private PlayerData currentPlayer;
    private DatabaseReference dbRef;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;

            dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
                if (task.IsCompleted && task.Result.Exists)
                {
                    currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                    Debug.Log("User data loaded via UID");
                    if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                }
            });
        }
    }

    public PlayerData GetCurrentPlayer() => currentPlayer;

    public void Login(string user, string pass, System.Action<bool> onResult)
    {
        Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithEmailAndPasswordAsync(user, pass).ContinueWithOnMainThread(authTask => {
            // I-check kung successful ang login task
            if (authTask.IsCompleted && !authTask.IsFaulted && !authTask.IsCanceled)
            {
                // TAMA NA SYNTAX: Gamitin ang authTask.Result.User.UserId
                string userId = authTask.Result.User.UserId;
                Debug.Log("Login Successful! UID: " + userId);

                // Dito na natin i-load ang data mula sa Realtime Database
                dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(dbTask => {
                    if (dbTask.IsCompleted && dbTask.Result.Exists)
                    {
                        currentPlayer = JsonUtility.FromJson<PlayerData>(dbTask.Result.GetRawJsonValue());
                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                        onResult?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogWarning("No database record found for this UID.");
                        onResult?.Invoke(false);
                    }
                });
            }
            else
            {
                Debug.LogError("Login Failed: " + authTask.Exception);
                onResult?.Invoke(false);
            }
        });
    }
    public void CreateAccountWithSecurity(string user, string pass, string q, string a, System.Action<bool> onResult)
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;

        // Note: Sa production, dapat may Register method ka sa Firebase Auth. 
        // Pero para sa logic mo ngayon, siguraduhin nating ang path ay UID:
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId; // Eto ang 'key' na kailangan ng Rules mo

            PlayerData newData = new PlayerData(user, pass)
            {
                securityQuestion = q,
                securityAnswer = a,
                username = user // Itabi pa rin ang username sa loob ng data
            };

            string json = JsonUtility.ToJson(newData);

            // Child(userId) na dapat, hindi Child(user)!
            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    currentPlayer = newData;
                    Debug.Log("Manual Account Created under UID: " + userId);
                    onResult?.Invoke(true);
                }
                else
                {
                    Debug.LogError("Database Error: " + task.Exception);
                    onResult?.Invoke(false);
                }
            });
        }
        else
        {
            Debug.LogError("No Authenticated User found! Login to Firebase Auth first.");
            onResult?.Invoke(false);
        }
    }

    // --- SECURITY & PASSWORD RESET ---
    public void ResetPassword(string user, string newPass)
    {
        if (currentPlayer != null && currentPlayer.username == user)
        {
            currentPlayer.passwordHash = newPass;
            SavePlayerProgress();
        }
    }

    public bool VerifySecurityAnswer(string user, string answer)
    {
        return currentPlayer != null && currentPlayer.securityAnswer == answer;
    }

    // --- GAMEPLAY & PROGRESS ---
    public void UnlockNextLevel()
    {
        if (currentPlayer != null)
        {
            currentPlayer.lastCompletedLevel++;
            currentPlayer.unlockedLevels++;
            SavePlayerProgress();
        }
    }

    public void CompletePuzzle(string puzzleId)
    {
        if (currentPlayer != null && !currentPlayer.completedPuzzles.Contains(puzzleId))
        {
            currentPlayer.completedPuzzles.Add(puzzleId);
            SavePlayerProgress();
        }
    }

    public void SavePlayerProgress()
    {
        if (currentPlayer == null) return;

        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            // UID ang gamitin para tanggapin ng Rules mo (auth.uid === $userId)
            string userId = auth.CurrentUser.UserId;
            string json = JsonUtility.ToJson(currentPlayer);

            // DITO DAPAT PAPASOK SA "users/UID"
            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
                if (task.IsCompleted) Debug.Log("Cloud Update Success!");
                else Debug.LogError("Cloud Update Failed: " + task.Exception);
            });
        }
    }

    public void LinkWithGoogle(string email, string gid, string name)
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null) return;

        string userId = auth.CurrentUser.UserId; // Gamitin ang UID para tumugma sa Rules mo

        // I-check kung may existing data na itong UID na ito
        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                // MERON NANG DATA: I-load ang existing progress
                currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                Debug.Log("Google Login: Existing user found. Loading progress...");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            }
            else
            {
                // WALA PANG DATA: Ito ang unang beses niya, gawan ng bagong entry
                currentPlayer = new PlayerData(email.Split('@')[0], "google_auth")
                {
                    googleId = gid,
                    googleEmail = email,
                    displayName = name
                };

                SavePlayerProgress(); // I-save ang initial data para hindi na siya 'null' next time
                Debug.Log("Google Login: New user created.");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            }
        });
    }

    public void Logout()
    {
        currentPlayer = null;
        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
    }
    public string GetSecurityQuestion(string username)
    {
        // Pansamantala itong hihingi sa current player o logic mo
        return (currentPlayer != null) ? currentPlayer.securityQuestion : "No question found";
    }
    public bool ResetPassword(string username, string newPassword, string securityAnswer)
    {
        if (currentPlayer != null && currentPlayer.username == username)
        {
            // I-verify muna ang answer bago i-reset
            if (currentPlayer.securityAnswer.Equals(securityAnswer, StringComparison.OrdinalIgnoreCase))
            {
                currentPlayer.passwordHash = newPassword;
                SavePlayerProgress();
                Debug.Log("Password reset successful!");
                return true; // Eto ang magpapa-true sa 'bool success'
            }
        }
        return false; // Failed reset
    }
    public bool IsPuzzleCompleted(string puzzleId)
    {
        return currentPlayer != null && currentPlayer.completedPuzzles.Contains(puzzleId);
    }
}