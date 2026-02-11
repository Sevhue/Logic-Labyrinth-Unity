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

    // --- AUTHENTICATION FUNCTIONS ---
    public void Login(string user, string pass, System.Action<bool> onResult)
    {
        dbRef.Child("users").Child(user).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted || task.Result.Value == null)
            {
                Debug.LogError("User not found!");
                onResult?.Invoke(false); // Ibalik ang 'false' sa UI
                return;
            }

            PlayerData data = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
            if (data.passwordHash == pass)
            {
                currentPlayer = data;
                UIManager.Instance.ShowMainMenu();
                onResult?.Invoke(true);
            }
            else
            {
                onResult?.Invoke(false);
            }
        });
    }

  public void CreateAccountWithSecurity(string user, string pass, string q, string a, System.Action<bool> onResult)
    {
        PlayerData newData = new PlayerData(user, pass)
        {
            securityQuestion = q,
            securityAnswer = a
        };
        string json = JsonUtility.ToJson(newData);
        dbRef.Child("users").Child(user).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
            if (task.IsCompleted)
            {
                currentPlayer = newData;
                onResult?.Invoke(true);
            }
         else
        {
            onResult?.Invoke(false);
        }
    });
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
            string userId = auth.CurrentUser.UserId;
            string json = JsonUtility.ToJson(currentPlayer);

            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
                if (task.IsFaulted) Debug.LogError("Save Failed: " + task.Exception);
                else Debug.Log("Progress Saved Successfully using UID!");
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