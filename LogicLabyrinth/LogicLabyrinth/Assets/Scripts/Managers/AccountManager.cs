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
        public string gender;
        public string age;
        public int unlockedLevels = 1;
        public int lastCompletedLevel = 0;

        public List<string> completedPuzzles = new List<string>();
        public List<string> destroyedGates = new List<string>();
        public List<string> collectedGates = new List<string>();
        public int andGatesCollected = 0;
        public int orGatesCollected = 0;
        public int notGatesCollected = 0;

        // Saved position & rotation for mid-level save
        public float savedPosX = 0f;
        public float savedPosY = 0f;
        public float savedPosZ = 0f;
        public float savedRotY = 0f;
        public int savedLevel = 0; // 0 means no mid-level save

        // Saved gate layout so the same types spawn at the same positions on Load Game.
        // Format: "AND,OR,NOT,OR,NOT" — one entry per spawn point in order.
        public string savedGateLayout = "";

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

        // 1. Show Main Login (Landing Page) first
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainLoginPanel();
        }

        // 2. AUTO-LOGIN CHECK: Only if there is a session
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;
            dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
                if (task.IsCompleted && task.Result.Exists)
                {
                    currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                    Debug.Log("Auto-login: Session found.");
                    Debug.Log($"Auto-login: Loaded gates - AND: {currentPlayer.andGatesCollected}, OR: {currentPlayer.orGatesCollected}, NOT: {currentPlayer.notGatesCollected}");

                    // NOTE: We do NOT sync to InventoryManager here.
                    // Inventory will be synced when the user clicks Load Game (ContinueGame).
                    // If they click New Game, everything gets reset anyway.

                    // Go to Main Menu if already logged in
                    if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                }
            });
        }
    }

    /// <summary>
    /// Re-fetch the latest player data from Firebase, then invoke the callback.
    /// This ensures we always have the freshest data (e.g., for Load Game).
    /// </summary>
    public void RefreshPlayerDataFromFirebase(System.Action<bool> onComplete)
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null)
        {
            Debug.LogWarning("RefreshPlayerData: No authenticated user.");
            onComplete?.Invoke(false);
            return;
        }

        string userId = auth.CurrentUser.UserId;
        Debug.Log("RefreshPlayerData: Fetching latest data from Firebase...");

        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                string rawJson = task.Result.GetRawJsonValue();
                Debug.Log($"[RefreshPlayerData] RAW JSON from Firebase:\n{rawJson}");

                currentPlayer = JsonUtility.FromJson<PlayerData>(rawJson);

                Debug.Log($"[RefreshPlayerData] Deserialized: gates AND={currentPlayer.andGatesCollected}, OR={currentPlayer.orGatesCollected}, NOT={currentPlayer.notGatesCollected}");
                Debug.Log($"[RefreshPlayerData] Deserialized: savedLevel={currentPlayer.savedLevel}, pos=({currentPlayer.savedPosX:F2},{currentPlayer.savedPosY:F2},{currentPlayer.savedPosZ:F2}), rotY={currentPlayer.savedRotY:F1}");
                Debug.Log($"[RefreshPlayerData] Deserialized: lastCompletedLevel={currentPlayer.lastCompletedLevel}, unlockedLevels={currentPlayer.unlockedLevels}");
                Debug.Log($"[RefreshPlayerData] Deserialized: destroyedGates count={currentPlayer.destroyedGates.Count}");
                if (currentPlayer.destroyedGates.Count > 0)
                {
                    foreach (var id in currentPlayer.destroyedGates)
                        Debug.Log($"[RefreshPlayerData]   destroyedGate: '{id}'");
                }

                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning("[RefreshPlayerData] No data found in Firebase (task completed but no data exists).");
                onComplete?.Invoke(false);
            }
        });
    }

    /// <summary>
    /// Syncs the InventoryManager with the current player's saved gate counts.
    /// Call this after loading player data, before entering a level.
    /// </summary>
    public void SyncInventoryFromPlayerData()
    {
        if (currentPlayer == null) return;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SyncFromCloud(
                currentPlayer.andGatesCollected,
                currentPlayer.orGatesCollected,
                currentPlayer.notGatesCollected
            );
            Debug.Log($"InventoryManager synced from player data - AND: {currentPlayer.andGatesCollected}, OR: {currentPlayer.orGatesCollected}, NOT: {currentPlayer.notGatesCollected}");
        }
        else
        {
            Debug.LogWarning("AccountManager: InventoryManager not available yet for sync.");
        }
    }

    public PlayerData GetCurrentPlayer() => currentPlayer;

    public void Login(string user, string pass, System.Action<bool> onResult)
    {
        // 1. Clean input
        string cleanUser = user.Trim().ToLower();

        // 2. Format email
        string emailToAuthenticate = cleanUser.Contains("@") ? cleanUser : cleanUser + "@logic.com";

        Debug.Log("DEBUG: Attempting login with: [" + emailToAuthenticate + "]");

        Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithEmailAndPasswordAsync(emailToAuthenticate, pass).ContinueWithOnMainThread(authTask => {

            if (authTask.IsCompleted && !authTask.IsFaulted && !authTask.IsCanceled)
            {
                string userId = authTask.Result.User.UserId;
                Debug.Log("Login Successful! UID: " + userId);

                dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(dbTask => {
                    if (dbTask.IsCompleted && dbTask.Result.Exists)
                    {
                        currentPlayer = JsonUtility.FromJson<PlayerData>(dbTask.Result.GetRawJsonValue());
                        Debug.Log($"Login: Loaded gates - AND: {currentPlayer.andGatesCollected}, OR: {currentPlayer.orGatesCollected}, NOT: {currentPlayer.notGatesCollected}");

                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                        onResult?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogWarning("No DB record found for: " + userId);
                        onResult?.Invoke(false);
                    }
                });
            }
            else
            {
                Debug.LogError("Firebase Auth Error: " + authTask.Exception.Flatten().InnerExceptions[0].Message);
                onResult?.Invoke(false);
            }
        });
    }
    public void CreateAccountWithSecurity(string user, string pass, string q, string a, string gender = "", string age = "", System.Action<bool, string> onResult = null)
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;

        // Sign out any previous user first to avoid session conflicts
        if (auth.CurrentUser != null)
        {
            Debug.Log("[AccountManager] Signing out previous user before creating new account.");
            auth.SignOut();
        }

        // Format email for Firebase Auth (username-only system, email is internal)
        string cleanUser = user.Trim().ToLower();
        string email = cleanUser.Contains("@") ? cleanUser : cleanUser + "@logic.com";

        Debug.Log($"[AccountManager] Creating account: email={email}, secQ={q}, gender={gender}, age={age}");

        // Create the Firebase Auth user first, then save data
        auth.CreateUserWithEmailAndPasswordAsync(email, pass).ContinueWithOnMainThread(authTask => {

            if (authTask.IsCanceled || authTask.IsFaulted)
            {
                string errorMsg = authTask.Exception?.Flatten()?.InnerExceptions?[0]?.Message ?? "Unknown error";

                // Parse specific Firebase error messages into user-friendly ones
                string userMessage = "Account creation failed";
                if (errorMsg.Contains("already in use"))
                    userMessage = "Username '" + user + "' is already taken. Please choose a different one.";
                else if (errorMsg.Contains("weak-password") || errorMsg.Contains("at least 6"))
                    userMessage = "Password must be at least 6 characters.";
                else
                    userMessage = errorMsg;

                Debug.LogError("Registration Failed: " + errorMsg);
                onResult?.Invoke(false, userMessage);
                return;
            }

            string userId = authTask.Result.User.UserId;

            PlayerData newData = new PlayerData(user, pass)
            {
                securityQuestion = q,
                securityAnswer = a,
                gender = gender,
                age = age,
                username = cleanUser.Replace("@logic.com", "")
            };

            string json = JsonUtility.ToJson(newData);
            Debug.Log($"[AccountManager] Saving player data: {json}");

            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(dbTask => {
                if (dbTask.IsCompleted)
                {
                    currentPlayer = newData;
                    Debug.Log("Account Created with Security under UID: " + userId);
                    UpdateLeaderboardEntry(userId); // Also write to leaderboard node
                    onResult?.Invoke(true, "Account created successfully!");
                }
                else
                {
                    Debug.LogError("Database Error: " + dbTask.Exception);
                    onResult?.Invoke(false, "Database error. Please try again.");
                }
            });
        });
    }
    public void CreateFullAccount(string user, string pass, string securityAnswer, string gender, string age, System.Action<bool> onResult)
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;

        auth.CreateUserWithEmailAndPasswordAsync(user, pass).ContinueWithOnMainThread(task => {

            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Registration Failed: " + task.Exception);
                onResult?.Invoke(false);
                return;
            }

            string userId = task.Result.User.UserId;

            PlayerData newData = new PlayerData(user, pass)
            {
                securityAnswer = securityAnswer,
                gender = gender,
                age = age,
                username = user.Replace("@logic.com", "")
            };

            string json = JsonUtility.ToJson(newData);

            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(dbTask => {
                if (dbTask.IsCompleted)
                {
                    currentPlayer = newData;
                    Debug.Log("Success! Account created and saved to DB.");
                    onResult?.Invoke(true);
                }
                else
                {
                    onResult?.Invoke(false);
                }
            });
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
        SavePlayerProgress(null);
    }

    /// <summary>
    /// Saves player data to Firebase. Optionally calls onComplete(true/false) when the write finishes.
    /// </summary>
    public void SavePlayerProgress(System.Action<bool> onComplete)
    {
        if (currentPlayer == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;
            string json = JsonUtility.ToJson(currentPlayer);

            Debug.Log($"[AccountManager] Saving to Firebase: {json}");

            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    Debug.Log("Cloud Update Success!");
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogError("Cloud Update Failed: " + task.Exception);
                    onComplete?.Invoke(false);
                }
            });

            // Also update the public leaderboard node (safe data only)
            UpdateLeaderboardEntry(userId);
        }
        else
        {
            Debug.LogWarning("[AccountManager] No authenticated user — cannot save.");
            onComplete?.Invoke(false);
        }
    }

    /// <summary>
    /// Writes only public-safe data to the "leaderboard" node.
    /// This node has open read access so the leaderboard panel can fetch it.
    /// </summary>
    private void UpdateLeaderboardEntry(string userId)
    {
        if (currentPlayer == null) return;

        var leaderboardData = new Dictionary<string, object>
        {
            { "username", currentPlayer.username ?? "Unknown" },
            { "lastCompletedLevel", currentPlayer.lastCompletedLevel },
            { "puzzlesCompleted", currentPlayer.completedPuzzles != null ? currentPlayer.completedPuzzles.Count : 0 }
        };

        dbRef.Child("leaderboard").Child(userId).UpdateChildrenAsync(leaderboardData).ContinueWithOnMainThread(task => {
            if (task.IsCompleted) Debug.Log("[Leaderboard] Public entry updated.");
            else Debug.LogWarning("[Leaderboard] Failed to update entry: " + task.Exception);
        });
    }

    public void LinkWithGoogle(string email, string gid, string name)
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null) return;

        string userId = auth.CurrentUser.UserId;

        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                Debug.Log("Google Login: Existing user found. Loading progress...");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            }
            else
            {
                currentPlayer = new PlayerData(email.Split('@')[0], "google_auth")
                {
                    googleId = gid,
                    googleEmail = email,
                    displayName = name
                };

                SavePlayerProgress();
                Debug.Log("Google Login: New user created.");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            }
        });
    }

    public void Logout()
    {
        // 1. Firebase Sign Out
        Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();

        // 2. Clear local player data
        currentPlayer = null;

        // 3. Reset inventory on logout
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ResetInventory();
        }

        // 4. Return to Main Login Screen
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainLoginPanel();
        }
    }
    public string GetSecurityQuestion(string username)
    {
        return (currentPlayer != null) ? currentPlayer.securityQuestion : "No question found";
    }
    public bool ResetPassword(string username, string newPassword, string securityAnswer)
    {
        if (currentPlayer != null && currentPlayer.username == username)
        {
            if (currentPlayer.securityAnswer.Equals(securityAnswer, StringComparison.OrdinalIgnoreCase))
            {
                currentPlayer.passwordHash = newPassword;
                SavePlayerProgress();
                Debug.Log("Password reset successful!");
                return true;
            }
        }
        return false;
    }
    public bool IsPuzzleCompleted(string puzzleId)
    {
        return currentPlayer != null && currentPlayer.completedPuzzles.Contains(puzzleId);
    }
}