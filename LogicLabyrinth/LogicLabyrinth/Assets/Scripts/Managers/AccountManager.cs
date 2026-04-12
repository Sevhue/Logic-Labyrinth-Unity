using UnityEngine;
using System;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Extensions;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance;
    private const string RealtimeDatabaseUrl = "https://logiclabyrinth-auth-default-rtdb.asia-southeast1.firebasedatabase.app";
    private const string SecurityAnswerIndexSalt = "logiclabyrinth-security-answer-v1";

    [Serializable]
    public class PlayerData
    {
        public string username;
        public string usernameLower;
        public string passwordHash;  // SHA-256 hash (not plain text)
        public string passwordSalt;  // Random salt used for hashing
        public string securityQuestion;
        public string securityAnswer;
        public string securityAnswerHashIndex;
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

        // Store purchases
        public bool hasScanner = false;
        public bool hasLantern = false;
        public int adrenalineCount = 0;

        // Saved position & rotation for mid-level save
        public float savedPosX = 0f;
        public float savedPosY = 0f;
        public float savedPosZ = 0f;
        public float savedRotY = 0f;
        public int savedLevel = 0; // 0 means no mid-level save

        // Saved gate layout so the same types spawn at the same positions on Load Game.
        // Format: "AND,OR,NOT,OR,NOT" — one entry per spawn point in order.
        public string savedGateLayout = "";

        // Profile picture name (matches a file in Resources/ProfilePictures/)
        public string profilePicture = "image-removebg-preview";

        // ── Timing / Leaderboard ──
        // Per-level best times stored as "level:seconds" pairs.
        // Format: "1:45.23,2:120.5,3:88.1" — comma-separated, level:bestTimeInSeconds
        public string bestLevelTimes = "";

        // Per-level completion attempts stored as "level:count" pairs.
        // Format: "2:4,3:2" — comma-separated, level:attemptCount
        public string levelAttemptCounts = "";

        // Total time spent playing across all sessions (seconds)
        public float totalPlayedSeconds = 0f;

        public PlayerData(string user, string pass, bool alreadyHashed = false)
        {
            this.username = user;
            this.usernameLower = AccountManager.NormalizeUsernameForLookup(user);
            if (alreadyHashed || pass == "google_auth")
            {
                // Already hashed or a special marker — store as-is
                this.passwordHash = pass;
                this.passwordSalt = "";
            }
            else
            {
                // Hash the plain-text password with a fresh random salt
                var (hash, salt) = PasswordHasher.HashNewPassword(pass);
                this.passwordHash = hash;
                this.passwordSalt = salt;
            }
        }
    }

    private PlayerData currentPlayer;
    private DatabaseReference dbRef;
    private string lastUsernameLookupError = "";
    private string lastKnownUserId = "";

    private const string InternalEmailSuffix = "@logic.com";
    private const string SessionLoggedOutKey = "LL_EXPLICIT_LOGOUT";
    private const string SessionPlayerJsonKey = "LL_LAST_PLAYER_JSON";
    private const string SessionPendingCloudSyncKey = "LL_PENDING_CLOUD_SYNC";
    private static bool offlineSaveWarningShown = false;

    private void CacheLastKnownUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        lastKnownUserId = userId;
    }

    private void MarkPendingCloudSync(bool pending)
    {
        PlayerPrefs.SetInt(SessionPendingCloudSyncKey, pending ? 1 : 0);
        PlayerPrefs.Save();
    }

    private bool HasPendingCloudSync()
    {
        return PlayerPrefs.GetInt(SessionPendingCloudSyncKey, 0) == 1;
    }

    private void TryFlushPendingCloudSync(string reason)
    {
        if (!HasPendingCloudSync()) return;

        if (currentPlayer == null)
            TryRestoreLocalSessionSnapshot();

        if (currentPlayer == null) return;
        if (!TryGetAuth(out var auth) || dbRef == null || auth == null || auth.CurrentUser == null) return;

        Debug.Log($"[AccountManager] Pending cloud sync detected ({reason}). Uploading local progress to Firebase/leaderboard...");
        SavePlayerProgress(success =>
        {
            if (success)
            {
                MarkPendingCloudSync(false);
                offlineSaveWarningShown = false;
                Debug.Log("[AccountManager] Pending cloud sync completed.");
            }
            else
            {
                MarkPendingCloudSync(true);
                Debug.LogWarning("[AccountManager] Pending cloud sync failed. Will retry next authenticated session.");
            }
        });
    }

    private void MarkExplicitLogout(bool loggedOut)
    {
        PlayerPrefs.SetInt(SessionLoggedOutKey, loggedOut ? 1 : 0);
        PlayerPrefs.Save();
    }

    private bool WasExplicitlyLoggedOut()
    {
        return PlayerPrefs.GetInt(SessionLoggedOutKey, 0) == 1;
    }

    private void PersistLocalSessionSnapshot()
    {
        if (currentPlayer == null) return;
        string json = JsonUtility.ToJson(currentPlayer);
        if (string.IsNullOrEmpty(json)) return;

        PlayerPrefs.SetString(SessionPlayerJsonKey, json);
        MarkExplicitLogout(false);
    }

    private bool TryRestoreLocalSessionSnapshot()
    {
        string json = PlayerPrefs.GetString(SessionPlayerJsonKey, "");
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var restored = JsonUtility.FromJson<PlayerData>(json);
            if (restored == null || string.IsNullOrWhiteSpace(restored.username))
                return false;

            currentPlayer = restored;
            NormalizePlayerIdentityFields(currentPlayer);
            Debug.Log("[AccountManager] Restored local session snapshot.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AccountManager] Failed to restore local session snapshot: " + ex.Message);
            return false;
        }
    }

    private void ClearLocalSessionSnapshot()
    {
        PlayerPrefs.DeleteKey(SessionPlayerJsonKey);
        PlayerPrefs.Save();
    }

    private static string BuildLookupErrorMessage(Exception ex)
    {
        if (ex == null) return "";

        string msg = ex.ToString();
        if (!string.IsNullOrEmpty(msg) && msg.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Permission denied by Firebase Rules.";

        return "Firebase lookup failed.";
    }

    public string GetLastUsernameLookupError()
    {
        return lastUsernameLookupError;
    }

    private static string HashSecurityAnswerForIndex(string answer)
    {
        if (answer == null) answer = "";
        return PasswordHasher.HashPassword(answer, SecurityAnswerIndexSalt);
    }

    private static bool IsLikelySha256Hex(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isHex = (c >= '0' && c <= '9') ||
                         (c >= 'a' && c <= 'f') ||
                         (c >= 'A' && c <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    private static bool VerifySecurityAnswerValue(PlayerData data, string typedAnswer)
    {
        if (data == null) return false;

        if (!string.IsNullOrEmpty(data.securityAnswerHashIndex))
        {
            string typedHash = HashSecurityAnswerForIndex(typedAnswer);
            return string.Equals(data.securityAnswerHashIndex, typedHash, StringComparison.OrdinalIgnoreCase);
        }

        string stored = data.securityAnswer ?? "";
        if (IsLikelySha256Hex(stored))
        {
            string typedHash = HashSecurityAnswerForIndex(typedAnswer);
            return string.Equals(stored, typedHash, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(stored, typedAnswer, StringComparison.Ordinal);
    }

    private void UpsertPasswordResetIndex(string userId, PlayerData data)
    {
        if (dbRef == null || string.IsNullOrWhiteSpace(userId) || data == null) return;

        string normalized = NormalizeUsernameForLookup(data.username);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = NormalizeUsernameForLookup(data.usernameLower);
        if (string.IsNullOrWhiteSpace(normalized)) return;

        string answerHash = HashSecurityAnswerForIndex(data.securityAnswer ?? "");
        data.securityAnswerHashIndex = answerHash;

        var resetEntry = new Dictionary<string, object>
        {
            { "uid", userId },
            { "usernameLower", normalized },
            { "securityQuestion", data.securityQuestion ?? "" },
            { "securityAnswerHash", answerHash }
        };

        dbRef.Child("passwordResetIndex").Child(userId).UpdateChildrenAsync(resetEntry)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                    Debug.LogWarning("[AccountManager] Failed to update passwordResetIndex: " + task.Exception);
            });
    }

    private static string NormalizeUsernameForLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.EndsWith(InternalEmailSuffix))
            normalized = normalized.Substring(0, normalized.Length - InternalEmailSuffix.Length);

        return normalized;
    }

    private static void NormalizePlayerIdentityFields(PlayerData data)
    {
        if (data == null) return;

        string normalized = NormalizeUsernameForLookup(data.username);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = NormalizeUsernameForLookup(data.usernameLower);

        data.usernameLower = normalized;
    }

    private static bool UsernamesMatch(PlayerData data, string normalizedLookup)
    {
        if (data == null || string.IsNullOrEmpty(normalizedLookup)) return false;

        if (NormalizeUsernameForLookup(data.username) == normalizedLookup) return true;
        if (NormalizeUsernameForLookup(data.usernameLower) == normalizedLookup) return true;

        return false;
    }

    private static Firebase.Database.DataSnapshot GetSnapshotAtPath(Firebase.Database.DataSnapshot root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;

        Firebase.Database.DataSnapshot node = root;
        string[] parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            node = node.Child(parts[i]);
            if (node == null || !node.Exists) return null;
        }

        return node;
    }

    private static string GetSnapshotString(Firebase.Database.DataSnapshot root, string path)
    {
        var node = GetSnapshotAtPath(root, path);
        if (node == null || !node.Exists || node.Value == null) return "";
        return node.Value.ToString();
    }

    private static bool SnapshotUsernameMatches(Firebase.Database.DataSnapshot userSnapshot, string normalizedLookup)
    {
        if (userSnapshot == null || string.IsNullOrEmpty(normalizedLookup)) return false;

        string[] usernamePaths =
        {
            "username",
            "usernameLower",
            "profile/username",
            "profile/usernameLower"
        };

        for (int i = 0; i < usernamePaths.Length; i++)
        {
            string candidate = NormalizeUsernameForLookup(GetSnapshotString(userSnapshot, usernamePaths[i]));
            if (candidate == normalizedLookup) return true;
        }

        return false;
    }

    private PlayerData BuildPlayerDataFromSnapshot(Firebase.Database.DataSnapshot userSnapshot, string normalizedLookup)
    {
        if (userSnapshot == null || !userSnapshot.Exists) return null;

        PlayerData data = null;
        string rawJson = userSnapshot.GetRawJsonValue();
        if (!string.IsNullOrEmpty(rawJson))
            data = JsonUtility.FromJson<PlayerData>(rawJson);

        string usernameFromSnapshot = GetSnapshotString(userSnapshot, "username");
        if (string.IsNullOrWhiteSpace(usernameFromSnapshot))
            usernameFromSnapshot = GetSnapshotString(userSnapshot, "profile/username");

        if (data == null)
        {
            string fallbackName = string.IsNullOrWhiteSpace(usernameFromSnapshot)
                ? normalizedLookup
                : usernameFromSnapshot;
            data = new PlayerData(fallbackName, "", true);
        }

        if (string.IsNullOrWhiteSpace(data.username))
            data.username = usernameFromSnapshot;

        if (string.IsNullOrWhiteSpace(data.usernameLower))
            data.usernameLower = NormalizeUsernameForLookup(string.IsNullOrWhiteSpace(data.username)
                ? normalizedLookup
                : data.username);

        if (string.IsNullOrWhiteSpace(data.securityQuestion))
            data.securityQuestion = GetSnapshotString(userSnapshot, "securityQuestion");
        if (string.IsNullOrWhiteSpace(data.securityQuestion))
            data.securityQuestion = GetSnapshotString(userSnapshot, "profile/securityQuestion");

        if (string.IsNullOrWhiteSpace(data.securityAnswer))
            data.securityAnswer = GetSnapshotString(userSnapshot, "securityAnswer");
        if (string.IsNullOrWhiteSpace(data.securityAnswer))
            data.securityAnswer = GetSnapshotString(userSnapshot, "profile/securityAnswer");

        if (string.IsNullOrWhiteSpace(data.passwordHash))
            data.passwordHash = GetSnapshotString(userSnapshot, "passwordHash");
        if (string.IsNullOrWhiteSpace(data.passwordHash))
            data.passwordHash = GetSnapshotString(userSnapshot, "profile/passwordHash");

        if (string.IsNullOrWhiteSpace(data.passwordSalt))
            data.passwordSalt = GetSnapshotString(userSnapshot, "passwordSalt");
        if (string.IsNullOrWhiteSpace(data.passwordSalt))
            data.passwordSalt = GetSnapshotString(userSnapshot, "profile/passwordSalt");

        // Legacy compatibility: some early records stored plain password in "password".
        if (string.IsNullOrWhiteSpace(data.passwordHash))
            data.passwordHash = GetSnapshotString(userSnapshot, "password");
        if (string.IsNullOrWhiteSpace(data.passwordHash))
            data.passwordHash = GetSnapshotString(userSnapshot, "profile/password");

        NormalizePlayerIdentityFields(data);
        return data;
    }

    private bool TryBuildFromResetIndexSnapshot(Firebase.Database.DataSnapshot snapshot, string normalizedLookup,
        out PlayerData data, out Firebase.Database.DatabaseReference userRef)
    {
        data = null;
        userRef = null;

        if (snapshot == null || !snapshot.Exists) return false;

        string uid = snapshot.Key;
        if (string.IsNullOrWhiteSpace(uid))
            uid = GetSnapshotString(snapshot, "uid");
        if (string.IsNullOrWhiteSpace(uid)) return false;

        string question = GetSnapshotString(snapshot, "securityQuestion");
        string answerHash = GetSnapshotString(snapshot, "securityAnswerHash");
        if (string.IsNullOrWhiteSpace(answerHash)) return false;

        data = new PlayerData(normalizedLookup, "", true)
        {
            username = normalizedLookup,
            usernameLower = normalizedLookup,
            securityQuestion = question,
            securityAnswerHashIndex = answerHash
        };

        userRef = dbRef.Child("users").Child(uid);
        return true;
    }

    private void BackfillNormalizedUsernameAsync(Firebase.Database.DatabaseReference userRef, PlayerData data, string normalizedLookup)
    {
        if (userRef == null || data == null || string.IsNullOrEmpty(normalizedLookup)) return;
        if (NormalizeUsernameForLookup(data.usernameLower) == normalizedLookup) return;

        data.usernameLower = normalizedLookup;
        string json = JsonUtility.ToJson(data);
        userRef.SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                Debug.Log($"[AccountManager] Backfilled usernameLower for '{data.username}'.");
            else
                Debug.LogWarning("[AccountManager] Failed to backfill usernameLower: " + task.Exception);
        });
    }

    private bool TryGetAuth(out Firebase.Auth.FirebaseAuth auth)
    {
        auth = null;
        try
        {
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            return auth != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AccountManager] Firebase Auth unavailable. Running in local/offline mode. " + ex.Message);
            return false;
        }
    }

    private void EnsureOfflineGuestPlayer()
    {
        if (currentPlayer != null) return;

        currentPlayer = new PlayerData("offline_guest", "offline_guest", true)
        {
            username = "offline_guest",
            displayName = "Offline Guest",
            unlockedLevels = 99
        };
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                dbRef = FirebaseDatabase.GetInstance(RealtimeDatabaseUrl).RootReference;
                Debug.Log("[AccountManager] Using RTDB URL: " + RealtimeDatabaseUrl);
            }
            catch (Exception ex)
            {
                dbRef = null;
                Debug.LogWarning("[AccountManager] Firebase Database unavailable. Running in local/offline mode. " + ex.Message);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        if (!TryGetAuth(out var auth) || auth == null)
        {
            // No auth service available; keep prior local session unless user explicitly logged out.
            if (!WasExplicitlyLoggedOut() && TryRestoreLocalSessionSnapshot())
            {
                TryFlushPendingCloudSync("startup-local-restore");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                return;
            }

            EnsureOfflineGuestPlayer();
            if (UIManager.Instance != null) UIManager.Instance.ShowMainLoginPanel();
            return;
        }

        // AUTO-LOGIN CHECK: Firebase session first
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;
            dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
                if (task.IsCompleted && task.Result.Exists)
                {
                    currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                    NormalizePlayerIdentityFields(currentPlayer);
                    PersistLocalSessionSnapshot();
                    TryFlushPendingCloudSync("startup-auto-login");
                    Debug.Log("Auto-login: Session found.");
                    Debug.Log($"Auto-login: Loaded gates - AND: {currentPlayer.andGatesCollected}, OR: {currentPlayer.orGatesCollected}, NOT: {currentPlayer.notGatesCollected}");

                    // NOTE: We do NOT sync to InventoryManager here.
                    // Inventory will be synced when the user clicks Load Game (ContinueGame).
                    // If they click New Game, everything gets reset anyway.

                    // Check if profile is incomplete (missing name/gender/age)
                    if (NewPlayerSetupUI.IsProfileIncomplete())
                    {
                        Debug.Log("Auto-login: Profile incomplete — showing setup UI.");
                        NewPlayerSetupUI.Show(() =>
                        {
                            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                        });
                    }
                    else
                    {
                        // Go to Main Menu if already logged in
                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                    }
                }
                else
                {
                    if (!WasExplicitlyLoggedOut() && TryRestoreLocalSessionSnapshot())
                    {
                        TryFlushPendingCloudSync("startup-fallback-local");
                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                    }
                    else if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowMainLoginPanel();
                    }
                }
            });
            return;
        }

        // No Firebase session; if user did not log out explicitly, restore local snapshot.
        if (!WasExplicitlyLoggedOut() && TryRestoreLocalSessionSnapshot())
        {
            TryFlushPendingCloudSync("startup-no-firebase-session");
            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            return;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainLoginPanel();
    }

    /// <summary>
    /// Re-fetch the latest player data from Firebase, then invoke the callback.
    /// This ensures we always have the freshest data (e.g., for Load Game).
    /// </summary>
    public void RefreshPlayerDataFromFirebase(System.Action<bool> onComplete)
    {
        if (!TryGetAuth(out var auth) || dbRef == null)
        {
            EnsureOfflineGuestPlayer();
            onComplete?.Invoke(false);
            return;
        }

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
                NormalizePlayerIdentityFields(currentPlayer);
                MarkExplicitLogout(false);
                PersistLocalSessionSnapshot();
                TryFlushPendingCloudSync("refresh-player-data");

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
        Login(user, pass, (success, _message) => onResult?.Invoke(success));
    }

    public void Login(string user, string pass, System.Action<bool, string> onResult)
    {
        if (!TryGetAuth(out var auth) || dbRef == null)
        {
            if (!WasExplicitlyLoggedOut() && TryRestoreLocalSessionSnapshot())
            {
                TryFlushPendingCloudSync("login-local-restore");
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                onResult?.Invoke(true, "Welcome back!");
                return;
            }

            onResult?.Invoke(false, "Login service unavailable. Please try again.");
            return;
        }

        // 1. Clean input
        string cleanUser = user.Trim().ToLower();

        // 2. Format email
        string emailToAuthenticate = cleanUser.Contains("@") ? cleanUser : cleanUser + "@logic.com";

        Debug.Log("DEBUG: Attempting login with: [" + emailToAuthenticate + "]");

        auth.SignInWithEmailAndPasswordAsync(emailToAuthenticate, pass).ContinueWithOnMainThread(authTask => {

            if (authTask.IsCompleted && !authTask.IsFaulted && !authTask.IsCanceled)
            {
                string userId = authTask.Result.User.UserId;
                Debug.Log("Login Successful! UID: " + userId);

                dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(dbTask => {
                    if (dbTask.IsCompleted && dbTask.Result.Exists)
                    {
                        currentPlayer = JsonUtility.FromJson<PlayerData>(dbTask.Result.GetRawJsonValue());
                        CacheLastKnownUserId(userId);
                        NormalizePlayerIdentityFields(currentPlayer);
                        MarkExplicitLogout(false);
                        PersistLocalSessionSnapshot();
                        TryFlushPendingCloudSync("login-success");
                        Debug.Log($"Login: Loaded gates - AND: {currentPlayer.andGatesCollected}, OR: {currentPlayer.orGatesCollected}, NOT: {currentPlayer.notGatesCollected}");

                        // Check if profile is incomplete (missing name/gender/age)
                        if (NewPlayerSetupUI.IsProfileIncomplete())
                        {
                            Debug.Log("Login: Profile incomplete — showing setup UI.");
                            NewPlayerSetupUI.Show(() =>
                            {
                                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                            });
                        }
                        else
                        {
                            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                        }
                        onResult?.Invoke(true, "Welcome back!");
                    }
                    else
                    {
                        Debug.LogWarning("No DB record found for: " + userId);
                        onResult?.Invoke(false, "Login failed. Player profile was not found.");
                    }
                });
            }
            else
            {
                string firebaseError = authTask.Exception?.Flatten()?.InnerExceptions?[0]?.Message ?? "Unknown Firebase auth error";

                // Forgot-password for logged-out users updates DB hash/salt, not Firebase Auth password.
                // Fallback to DB credential verification so reset users can still sign in.
                TryDatabaseCredentialLogin(cleanUser, pass, (success, fallbackMessage) =>
                {
                    if (!success)
                    {
                        // Wrong credentials are expected during normal login attempts; keep logs readable.
                        if (IsLikelyInvalidCredentialsAuthError(firebaseError))
                            Debug.LogWarning("[AccountManager] Login failed due to invalid credentials.");
                        else
                            Debug.LogWarning("[AccountManager] Firebase Auth sign-in failed: " + firebaseError);

                        onResult?.Invoke(false,
                            string.IsNullOrWhiteSpace(fallbackMessage)
                                ? "Incorrect username or password."
                                : fallbackMessage);
                    }
                    else
                    {
                        Debug.LogWarning("Firebase Auth sign-in failed, but DB fallback login succeeded.");
                        MarkExplicitLogout(false);
                        PersistLocalSessionSnapshot();
                        TryFlushPendingCloudSync("login-db-fallback-success");
                        TryRecoverFirebaseAuthSessionFromFallback(cleanUser, pass);
                        onResult?.Invoke(true, "Welcome back!");
                    }
                });
            }
        });
    }

    private void TryDatabaseCredentialLogin(string normalizedUsername, string password, System.Action<bool, string> onResult)
    {
        if (dbRef == null)
        {
            Debug.LogWarning("[AccountManager] DB login fallback aborted: dbRef is null.");
            onResult?.Invoke(false, "Login service unavailable. Please try again.");
            return;
        }

        FindUserByUsernameAsync(normalizedUsername, (data, userRef) =>
        {
            if (data == null || userRef == null)
            {
                Debug.LogWarning($"[AccountManager] DB login fallback failed: user '{normalizedUsername}' not found. LookupError='{lastUsernameLookupError}'.");

                if (!string.IsNullOrEmpty(lastUsernameLookupError) &&
                    lastUsernameLookupError.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    onResult?.Invoke(false, "Login service unavailable. Please try again.");
                }
                else
                {
                    onResult?.Invoke(false, "No account found for that username.");
                }

                return;
            }

            bool passwordMatches = PasswordHasher.VerifyPassword(password, data.passwordHash, data.passwordSalt);
            if (!passwordMatches)
            {
                // Legacy compatibility: very old records may still have plain text in passwordHash.
                passwordMatches = string.Equals(data.passwordHash ?? "", password, StringComparison.Ordinal);
            }

            if (!passwordMatches)
            {
                string hashPreview = string.IsNullOrEmpty(data.passwordHash)
                    ? "<empty>"
                    : data.passwordHash.Substring(0, Math.Min(8, data.passwordHash.Length));
                string saltState = string.IsNullOrEmpty(data.passwordSalt) ? "missing" : "present";
                Debug.LogWarning($"[AccountManager] DB login fallback password mismatch for '{normalizedUsername}' (hash={hashPreview}..., salt={saltState}).");
                onResult?.Invoke(false, "Wrong password.");
                return;
            }

            NormalizePlayerIdentityFields(data);
            currentPlayer = data;
            CacheLastKnownUserId(userRef.Key);
            MarkExplicitLogout(false);
            PersistLocalSessionSnapshot();
            TryFlushPendingCloudSync("login-db-credential-fallback");
            Debug.Log("[AccountManager] Login fallback succeeded via DB password hash verification.");

            if (NewPlayerSetupUI.IsProfileIncomplete())
            {
                NewPlayerSetupUI.Show(() =>
                {
                    if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                });
            }
            else
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            }

            onResult?.Invoke(true, "Welcome back!");
        });
    }

    private static bool IsLikelyInvalidCredentialsAuthError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage)) return false;

        string msg = errorMessage.ToLowerInvariant();
        return msg.Contains("password is invalid") ||
               msg.Contains("no user record") ||
               msg.Contains("user-not-found") ||
               msg.Contains("wrong-password") ||
               msg.Contains("invalid login credentials") ||
               msg.Contains("invalid email") ||
               msg.Contains("internal error");
    }

    private void TryRecoverFirebaseAuthSessionFromFallback(string normalizedUsername, string password)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password)) return;
        if (!TryGetAuth(out var auth) || auth == null || dbRef == null) return;
        if (auth.CurrentUser != null)
        {
            PromoteCurrentSessionToUidSync(auth.CurrentUser.UserId, "fallback-recover-existing-auth");
            return;
        }

        string email = normalizedUsername.Contains("@")
            ? normalizedUsername.Trim().ToLowerInvariant()
            : (normalizedUsername.Trim().ToLowerInvariant() + InternalEmailSuffix);

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(signInTask =>
        {
            if (signInTask.IsCompleted && !signInTask.IsFaulted && !signInTask.IsCanceled)
            {
                string uid = signInTask.Result.User.UserId;
                PromoteCurrentSessionToUidSync(uid, "fallback-recover-signin-success");
                return;
            }

            if (!IsAuthUserNotFound(signInTask.Exception))
            {
                Debug.LogWarning("[AccountManager] Fallback auth recovery sign-in failed (non-user-not-found). Continuing in fallback mode.");
                return;
            }

            auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(createTask =>
            {
                if (createTask.IsCompleted && !createTask.IsFaulted && !createTask.IsCanceled)
                {
                    string uid = createTask.Result.User.UserId;
                    PromoteCurrentSessionToUidSync(uid, "fallback-recover-create-success");
                }
                else
                {
                    Debug.LogWarning("[AccountManager] Fallback auth recovery create-user failed. Continuing in fallback mode. " + createTask.Exception);
                }
            });
        });
    }

    private void PromoteCurrentSessionToUidSync(string userId, string reason)
    {
        if (currentPlayer == null || dbRef == null || string.IsNullOrWhiteSpace(userId)) return;

        CacheLastKnownUserId(userId);

        NormalizePlayerIdentityFields(currentPlayer);
        string json = JsonUtility.ToJson(currentPlayer);

        dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
            {
                MarkPendingCloudSync(false);
                offlineSaveWarningShown = false;
                PersistLocalSessionSnapshot();
                UpdateLeaderboardEntry(userId);
                RemoveLegacyPublicLeaderboardEntry(userId);
                Debug.Log($"[AccountManager] Promoted fallback session to Firebase Auth UID sync ({reason}).");
            }
            else
            {
                Debug.LogWarning("[AccountManager] Failed to promote fallback session to UID sync: " + task.Exception);
            }
        });
    }

    private static bool IsAuthUserNotFound(Exception ex)
    {
        if (ex == null) return false;
        string msg = ex.ToString();
        return msg.IndexOf("no user record", StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("user-not-found", StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("email_not_found", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    public void CreateAccountWithSecurity(string user, string pass, string q, string a, string gender = "", string age = "", System.Action<bool, string> onResult = null)
    {
        if (!TryGetAuth(out var auth) || dbRef == null)
        {
            EnsureOfflineGuestPlayer();
            onResult?.Invoke(false, "Cloud account services unavailable (offline mode).");
            return;
        }

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
                username = cleanUser.Replace("@logic.com", ""),
                usernameLower = NormalizeUsernameForLookup(cleanUser)
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
        if (!TryGetAuth(out var auth) || dbRef == null)
        {
            EnsureOfflineGuestPlayer();
            onResult?.Invoke(false);
            return;
        }

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
                securityQuestion = "What is your security answer?",
                securityAnswer = securityAnswer,
                gender = gender,
                age = age,
                username = user.Replace("@logic.com", ""),
                usernameLower = NormalizeUsernameForLookup(user)
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
            var (hash, salt) = PasswordHasher.HashNewPassword(newPass);
            currentPlayer.passwordHash = hash;
            currentPlayer.passwordSalt = salt;
            SavePlayerProgress();
        }
    }

    public bool VerifySecurityAnswer(string user, string answer)
    {
        return currentPlayer != null && currentPlayer.securityAnswer.Equals(answer, StringComparison.Ordinal);
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

    // ── TIMING / BEST TIMES ──

    /// <summary>
    /// Records a completion time for a level. Only updates if it's a new best (lower time).
    /// Also adds to totalPlayedSeconds.
    /// </summary>
    public void RecordLevelTime(int level, float seconds)
    {
        if (currentPlayer == null)
        {
            Debug.LogWarning($"[AccountManager] Skipped RecordLevelTime for Level {level}: no logged-in player.");
            return;
        }

        // Track attempts for this level regardless of whether the time is a new best.
        var attemptCounts = ParseAttemptCounts(currentPlayer.levelAttemptCounts);
        attemptCounts[level] = attemptCounts.ContainsKey(level) ? attemptCounts[level] + 1 : 1;
        currentPlayer.levelAttemptCounts = SerializeAttemptCounts(attemptCounts);

        // Add to total played time
        currentPlayer.totalPlayedSeconds += seconds;

        // Parse existing best times
        var bestTimes = ParseBestTimes(currentPlayer.bestLevelTimes);

        // Check if this is a new best
        if (!bestTimes.ContainsKey(level) || seconds < bestTimes[level])
        {
            bestTimes[level] = seconds;
            currentPlayer.bestLevelTimes = SerializeBestTimes(bestTimes);
            Debug.Log($"[AccountManager] NEW BEST TIME for Level {level}: {LevelTimer.FormatTime(seconds)}!");
        }
        else
        {
            Debug.Log($"[AccountManager] Level {level} completed in {LevelTimer.FormatTime(seconds)} (best: {LevelTimer.FormatTime(bestTimes[level])})");
        }

        SavePlayerProgress();
    }

    /// <summary>
    /// Gets the best time for a specific level. Returns -1 if no time recorded.
    /// </summary>
    public float GetBestTime(int level)
    {
        if (currentPlayer == null) return -1f;
        var bestTimes = ParseBestTimes(currentPlayer.bestLevelTimes);
        return bestTimes.ContainsKey(level) ? bestTimes[level] : -1f;
    }

    /// <summary>
    /// Gets the sum of all best level times (used for global ranking).
    /// Returns -1 if no times are recorded.
    /// </summary>
    public float GetTotalBestTime()
    {
        if (currentPlayer == null) return -1f;
        var bestTimes = ParseBestTimes(currentPlayer.bestLevelTimes);
        if (bestTimes.Count == 0) return -1f;

        float total = 0f;
        foreach (var kvp in bestTimes)
            total += kvp.Value;
        return total;
    }

    /// <summary>
    /// Gets the fastest single level time across all levels.
    /// Returns -1 if no times are recorded.
    /// </summary>
    public float GetFastestLevelTime()
    {
        if (currentPlayer == null) return -1f;
        var bestTimes = ParseBestTimes(currentPlayer.bestLevelTimes);
        if (bestTimes.Count == 0) return -1f;

        float fastest = float.MaxValue;
        foreach (var kvp in bestTimes)
            if (kvp.Value < fastest) fastest = kvp.Value;
        return fastest;
    }

    /// <summary>
    /// Returns the number of levels that have a recorded best time.
    /// </summary>
    public int GetCompletedLevelCount()
    {
        if (currentPlayer == null) return 0;
        return ParseBestTimes(currentPlayer.bestLevelTimes).Count;
    }

    // ── BEST TIME SERIALIZATION ──

    /// <summary>
    /// Parses "1:45.23,2:120.5,3:88.1" into a Dictionary.
    /// </summary>
    public static Dictionary<int, float> ParseBestTimes(string serialized)
    {
        var result = new Dictionary<int, float>();
        if (string.IsNullOrWhiteSpace(serialized)) return result;

        string[] pairs = serialized.Split(',');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int lvl) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float time))
            {
                result[lvl] = time;
            }
        }
        return result;
    }

    /// <summary>
    /// Parses "2:4,3:2" into a Dictionary of per-level attempt counts.
    /// </summary>
    public static Dictionary<int, int> ParseAttemptCounts(string serialized)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(serialized)) return result;

        string[] pairs = serialized.Split(',');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int lvl) &&
                int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int count))
            {
                result[lvl] = Mathf.Max(0, count);
            }
        }

        return result;
    }

    /// <summary>
    /// Serializes a Dictionary into "1:45.23,2:120.5,3:88.1" format.
    /// </summary>
    private static string SerializeBestTimes(Dictionary<int, float> times)
    {
        var parts = new List<string>();
        foreach (var kvp in times)
        {
            parts.Add($"{kvp.Key}:{kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        return string.Join(",", parts);
    }

    private static string SerializeAttemptCounts(Dictionary<int, int> attempts)
    {
        var parts = new List<string>();
        foreach (var kvp in attempts)
        {
            parts.Add($"{kvp.Key}:{Mathf.Max(0, kvp.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        return string.Join(",", parts);
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

        if (!TryGetAuth(out var auth) || dbRef == null || auth.CurrentUser == null)
        {
            // Offline/local mode: keep gameplay functional without cloud persistence.
            PersistLocalSessionSnapshot();
            MarkPendingCloudSync(true);

            string fallbackUid = !string.IsNullOrWhiteSpace(lastKnownUserId)
                ? lastKnownUserId
                : (auth != null && auth.CurrentUser != null ? auth.CurrentUser.UserId : "");

            if (!string.IsNullOrWhiteSpace(fallbackUid) && dbRef != null)
            {
                NormalizePlayerIdentityFields(currentPlayer);
                string json = JsonUtility.ToJson(currentPlayer);

                dbRef.Child("users").Child(fallbackUid).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                    {
                        Debug.Log($"[AccountManager] Fallback UID sync succeeded for '{fallbackUid}'.");
                    }
                    else
                    {
                        Debug.LogWarning("[AccountManager] Fallback UID sync failed: " + task.Exception);
                    }
                });

                UpdateLeaderboardEntry(fallbackUid);
            }

            // Best-effort public leaderboard sync so profile/time changes are still visible globally.
            if (dbRef != null)
            {
                string publicKey = BuildPublicLeaderboardKey(currentPlayer);
                UpdateLeaderboardEntry(publicKey);
                Debug.Log($"[AccountManager] Offline/local mode: attempted public leaderboard sync using key '{publicKey}'.");
            }

            if (!offlineSaveWarningShown)
            {
                Debug.LogWarning("[AccountManager] Cloud save skipped (offline/local mode). Progress is saved locally; leaderboard/cloud sync will resume when authenticated online.");
                offlineSaveWarningShown = true;
            }
            onComplete?.Invoke(true);
            return;
        }

        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;
            CacheLastKnownUserId(userId);
            NormalizePlayerIdentityFields(currentPlayer);
            string json = JsonUtility.ToJson(currentPlayer);
            PersistLocalSessionSnapshot();

            Debug.Log($"[AccountManager] Saving to Firebase: {json}");

            dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task => {
                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    MarkPendingCloudSync(false);
                    offlineSaveWarningShown = false;
                    Debug.Log("Cloud Update Success!");
                    onComplete?.Invoke(true);
                }
                else
                {
                    MarkPendingCloudSync(true);
                    Debug.LogError("Cloud Update Failed: " + task.Exception);
                    onComplete?.Invoke(false);
                }
            });

            // Also update the public leaderboard node (safe data only)
            UpdateLeaderboardEntry(userId);
            RemoveLegacyPublicLeaderboardEntry(userId);
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
        if (currentPlayer == null || dbRef == null || string.IsNullOrWhiteSpace(userId)) return;

        // Prefer displayName for the leaderboard; fall back to username
        string leaderboardName = !string.IsNullOrWhiteSpace(currentPlayer.displayName)
            ? currentPlayer.displayName
            : (currentPlayer.username ?? "Unknown");

        // Calculate timing stats for leaderboard
        var bestTimes = ParseBestTimes(currentPlayer.bestLevelTimes);
        float totalBestTime = 0f;
        float fastestLevel = -1f;
        foreach (var kvp in bestTimes)
        {
            totalBestTime += kvp.Value;
            if (fastestLevel < 0f || kvp.Value < fastestLevel)
                fastestLevel = kvp.Value;
        }

        var leaderboardData = new Dictionary<string, object>
        {
            { "username", leaderboardName },
            { "lastCompletedLevel", currentPlayer.lastCompletedLevel },
            { "puzzlesCompleted", currentPlayer.completedPuzzles != null ? currentPlayer.completedPuzzles.Count : 0 },
            { "profilePicture", currentPlayer.profilePicture ?? "image-removebg-preview" },
            { "bestLevelTimes", currentPlayer.bestLevelTimes ?? "" },
            { "levelAttemptCounts", currentPlayer.levelAttemptCounts ?? "" },
            { "totalBestTime", bestTimes.Count > 0 ? totalBestTime : -1f },
            { "fastestLevelTime", fastestLevel },
            { "levelsCompleted", bestTimes.Count },
            { "totalPlayedSeconds", currentPlayer.totalPlayedSeconds },
            { "updatedAtUnixMs", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };

        dbRef.Child("leaderboard").Child(userId).UpdateChildrenAsync(leaderboardData).ContinueWithOnMainThread(task => {
            if (task.IsCompleted) Debug.Log("[Leaderboard] Public entry updated.");
            else Debug.LogWarning("[Leaderboard] Failed to update entry: " + task.Exception);
        });
    }

    private static string BuildPublicLeaderboardKey(PlayerData player)
    {
        if (player == null) return "public_guest";

        string normalized = NormalizeUsernameForLookup(player.username);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = NormalizeUsernameForLookup(player.displayName);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "guest";

        return "public_" + normalized;
    }

    private void RemoveLegacyPublicLeaderboardEntry(string authenticatedUserId)
    {
        if (dbRef == null || currentPlayer == null) return;

        string publicKey = BuildPublicLeaderboardKey(currentPlayer);
        if (string.IsNullOrWhiteSpace(publicKey) || publicKey == authenticatedUserId) return;

        dbRef.Child("leaderboard").Child(publicKey).RemoveValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                Debug.Log($"[Leaderboard] Removed legacy public fallback entry '{publicKey}'.");
        });
    }

    public void LinkWithGoogle(string email, string gid, string name)
    {
        if (!TryGetAuth(out var auth) || dbRef == null || auth.CurrentUser == null)
        {
            EnsureOfflineGuestPlayer();
            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
            return;
        }

        string userId = auth.CurrentUser.UserId;

        dbRef.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted && task.Result.Exists)
            {
                currentPlayer = JsonUtility.FromJson<PlayerData>(task.Result.GetRawJsonValue());
                NormalizePlayerIdentityFields(currentPlayer);
                TryFlushPendingCloudSync("link-google-existing-user");
                Debug.Log("Google Login: Existing user found. Loading progress...");

                // Check if profile is incomplete (missing name/gender/age)
                if (NewPlayerSetupUI.IsProfileIncomplete())
                {
                    Debug.Log("Google Login: Profile incomplete — showing setup UI.");
                    NewPlayerSetupUI.Show(() =>
                    {
                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                    });
                }
                else
                {
                    if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                }
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
                Debug.Log("Google Login: New user created — showing profile setup.");

                // New Google user — always show setup for gender/age
                NewPlayerSetupUI.Show(() =>
                {
                    if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                });
            }
        });
    }

    public void Logout()
    {
        // 1. Firebase Sign Out
        if (TryGetAuth(out var auth) && auth != null)
            auth.SignOut();

        MarkExplicitLogout(true);
        ClearLocalSessionSnapshot();

        // 2. Clear local player data
        currentPlayer = null;
        lastKnownUserId = "";

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
        if (currentPlayer != null &&
            currentPlayer.username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
            return currentPlayer.securityQuestion;
        return null;
    }

    /// <summary>
    /// Finds a user record by username (case-insensitive).
    /// First tries an indexed query with the lowercase name; if not found, falls back to a
    /// full scan so that accounts stored with mixed-case usernames are still matched.
    /// Calls callback(playerData, dbRef) — both null if no match.
    /// </summary>
    private void FindUserByUsernameAsync(string username,
        System.Action<PlayerData, Firebase.Database.DatabaseReference> callback)
    {
        lastUsernameLookupError = "";
        if (dbRef == null)
        {
            lastUsernameLookupError = "Firebase database is unavailable.";
            callback?.Invoke(null, null);
            return;
        }

        string normalizedLookup = NormalizeUsernameForLookup(username);
        if (string.IsNullOrEmpty(normalizedLookup))
        {
            lastUsernameLookupError = "Username is empty.";
            callback?.Invoke(null, null);
            return;
        }

        // Direct lookup path on users node.
        FindUserByUsersNodeFallbackAsync(normalizedLookup, callback);
    }

    private void FindUserByUsersNodeFallbackAsync(string normalizedLookup,
        System.Action<PlayerData, Firebase.Database.DatabaseReference> callback)
    {
        dbRef.Child("users").OrderByChild("usernameLower").EqualTo(normalizedLookup).LimitToFirst(1)
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    lastUsernameLookupError = BuildLookupErrorMessage(task.Exception);
                    Debug.LogWarning("[AccountManager] usernameLower lookup failed: " + task.Exception);

                    // Permission errors won't be fixed by fallback queries.
                    if (lastUsernameLookupError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                    {
                        callback?.Invoke(null, null);
                        return;
                    }
                }

                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled && task.Result != null && task.Result.Exists)
                {
                    foreach (var child in task.Result.Children)
                    {
                        PlayerData data = BuildPlayerDataFromSnapshot(child, normalizedLookup);
                        if (data != null)
                        {
                            callback?.Invoke(data, child.Reference);
                            return;
                        }
                    }
                }

                // Legacy fallback: old rows without usernameLower.
                FindUserByLegacyUsernameIndexAsync(normalizedLookup, callback);
            });
    }

    private void FindUserByLegacyUsernameIndexAsync(string normalizedLookup,
        System.Action<PlayerData, Firebase.Database.DatabaseReference> callback)
    {
        dbRef.Child("users").OrderByChild("username").EqualTo(normalizedLookup).LimitToFirst(1)
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    lastUsernameLookupError = BuildLookupErrorMessage(task.Exception);
                    Debug.LogWarning("[AccountManager] Legacy username lookup failed: " + task.Exception);

                    if (lastUsernameLookupError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                    {
                        callback?.Invoke(null, null);
                        return;
                    }
                }

                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled && task.Result != null && task.Result.Exists)
                {
                    foreach (var child in task.Result.Children)
                    {
                        PlayerData data = BuildPlayerDataFromSnapshot(child, normalizedLookup);
                        if (data != null)
                        {
                            BackfillNormalizedUsernameAsync(child.Reference, data, normalizedLookup);
                            callback?.Invoke(data, child.Reference);
                            return;
                        }
                    }
                }

                // Final fallback: full scan for mixed-case legacy usernames.
                FindUserByFullScanAsync(normalizedLookup, callback);
            });
    }

    private void FindUserByFullScanAsync(string normalizedLookup,
        System.Action<PlayerData, Firebase.Database.DatabaseReference> callback)
    {
        dbRef.Child("users").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                lastUsernameLookupError = BuildLookupErrorMessage(task.Exception);
                Debug.LogWarning("[AccountManager] Full-scan username lookup failed: " + task.Exception);
            }

            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled && task.Result != null && task.Result.Exists)
            {
                int scannedUsers = 0;
                foreach (var child in task.Result.Children)
                {
                    scannedUsers++;
                    if (SnapshotUsernameMatches(child, normalizedLookup))
                    {
                        PlayerData data = BuildPlayerDataFromSnapshot(child, normalizedLookup);
                        if (data == null) continue;

                        BackfillNormalizedUsernameAsync(child.Reference, data, normalizedLookup);
                        callback?.Invoke(data, child.Reference);
                        return;
                    }
                }

                Debug.Log($"[AccountManager] Full-scan checked {scannedUsers} users; no username match for '{normalizedLookup}'.");
            }

            if (string.IsNullOrEmpty(lastUsernameLookupError))
                lastUsernameLookupError = "Username not found.";

            Debug.LogWarning($"[AccountManager] Username '{normalizedLookup}' not found in Firebase.");
            callback?.Invoke(null, null);
        });
    }

    /// <summary>
    /// Async version: returns the security question for the given username.
    /// Works for both logged-in and logged-out users by querying Firebase DB when needed.
    /// </summary>
    public void GetSecurityQuestionForUserAsync(string username, System.Action<string> callback)
    {
        if (currentPlayer != null &&
            currentPlayer.username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            callback?.Invoke(currentPlayer.securityQuestion);
            return;
        }

        FindUserByUsernameAsync(username, (data, _) =>
        {
            if (data == null)
            {
                callback?.Invoke(null);
                return;
            }

            if (string.IsNullOrWhiteSpace(data.securityQuestion))
            {
                callback?.Invoke("Answer your security answer to continue.");
                return;
            }

            callback?.Invoke(data.securityQuestion);
        });
    }

    /// <summary>
    /// Async version: verifies the security answer for the given username.
    /// Works for both logged-in and logged-out users.
    /// </summary>
    public void VerifySecurityAnswerAsync(string username, string answer, System.Action<bool> callback)
    {
        string trimmedAnswer = answer.Trim();

        if (currentPlayer != null &&
            currentPlayer.username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            callback?.Invoke(currentPlayer.securityAnswer.Equals(trimmedAnswer, StringComparison.Ordinal));
            return;
        }

        FindUserByUsernameAsync(username, (data, _) =>
        {
            if (data == null) { callback?.Invoke(false); return; }
            callback?.Invoke(VerifySecurityAnswerValue(data, trimmedAnswer));
        });
    }

    /// <summary>
    /// Async password reset. Verifies security answer, updates Firebase Auth password (for
    /// logged-in users) and the database record. Calls callback(success, message) on completion.
    /// </summary>
    public void ResetPasswordAsync(string username, string newPassword, string securityAnswer,
        System.Action<bool, string> callback)
    {
        string trimmedAnswer = securityAnswer.Trim();

        // ── Case 1: Logged-in user changing their own password ──
        if (currentPlayer != null &&
            currentPlayer.username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (!currentPlayer.securityAnswer.Equals(trimmedAnswer, StringComparison.Ordinal))
            {
                callback?.Invoke(false, "Incorrect security answer.");
                return;
            }

            var (hash, salt) = PasswordHasher.HashNewPassword(newPassword);
            currentPlayer.passwordHash = hash;
            currentPlayer.passwordSalt = salt;

            if (TryGetAuth(out var auth) && auth.CurrentUser != null)
            {
                auth.CurrentUser.UpdatePasswordAsync(newPassword).ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                    {
                        SavePlayerProgress();
                        Debug.Log("[AccountManager] Firebase Auth password updated successfully.");
                        callback?.Invoke(true, "Password changed successfully!");
                    }
                    else
                    {
                        string err = task.Exception?.Flatten()?.InnerExceptions?[0]?.Message ?? "Unknown error";
                        Debug.LogError("[AccountManager] UpdatePasswordAsync failed: " + err);
                        if (err.Contains("requires-recent-login"))
                            callback?.Invoke(false, "Please log out and log back in before changing your password.");
                        else
                            callback?.Invoke(false, "Failed to update password. Please try again.");
                    }
                });
            }
            else
            {
                SavePlayerProgress();
                callback?.Invoke(true, "Password updated.");
            }
            return;
        }

        // ── Case 2: Non-logged-in user — look up by username in Firebase DB ──
        if (dbRef == null)
        {
            callback?.Invoke(false, "Service unavailable. Please try again later.");
            return;
        }

        FindUserByUsernameAsync(username, (data, userRef) =>
        {
            if (data == null || userRef == null)
            {
                if (!string.IsNullOrEmpty(lastUsernameLookupError) &&
                    lastUsernameLookupError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                {
                    callback?.Invoke(false, "Cannot reset password: Firebase Rules blocked username lookup (permission denied).");
                }
                else
                {
                    callback?.Invoke(false, "Username not found.");
                }
                return;
            }

            bool answerOk = VerifySecurityAnswerValue(data, trimmedAnswer);

            if (!answerOk)
            {
                callback?.Invoke(false, "Incorrect security answer.");
                return;
            }

            var (hash, salt) = PasswordHasher.HashNewPassword(newPassword);
            data.passwordHash = hash;
            data.passwordSalt = salt;
            NormalizePlayerIdentityFields(data);

            var passwordUpdate = new Dictionary<string, object>
            {
                { "passwordHash", hash },
                { "passwordSalt", salt }
            };

            var setHashTask = userRef.Child("passwordHash").SetValueAsync(hash);
            var setSaltTask = userRef.Child("passwordSalt").SetValueAsync(salt);

            System.Threading.Tasks.Task.WhenAll(setHashTask, setSaltTask).ContinueWithOnMainThread(dbTask =>
            {
                if (dbTask.IsCompleted && !dbTask.IsFaulted)
                {
                    Debug.Log("[AccountManager] Password reset for non-logged-in user saved to DB.");
                    callback?.Invoke(true, "Password has been reset. You can now log in with your new password.");
                }
                else
                {
                    Debug.LogError("[AccountManager] Failed to save password reset: " + dbTask.Exception);
                    if (dbTask.Exception != null &&
                        dbTask.Exception.ToString().IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        callback?.Invoke(false, "Password reset blocked by Firebase Rules (permission denied).");
                    }
                    else
                    {
                        callback?.Invoke(false, "Failed to save new password. Please try again.");
                    }
                }
            });
        });
    }

    public bool ResetPassword(string username, string newPassword, string securityAnswer)
    {
        if (currentPlayer != null && currentPlayer.username == username)
        {
            if (currentPlayer.securityAnswer.Equals(securityAnswer, StringComparison.OrdinalIgnoreCase))
            {
                var (hash, salt) = PasswordHasher.HashNewPassword(newPassword);
                currentPlayer.passwordHash = hash;
                currentPlayer.passwordSalt = salt;
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

    public bool HasStoreItem(string itemId)
    {
        if (currentPlayer == null || string.IsNullOrWhiteSpace(itemId)) return false;

        switch (itemId.Trim().ToLowerInvariant())
        {
            case "scanner":
                return currentPlayer.hasScanner;
            case "lantern":
                return currentPlayer.hasLantern;
            case "adrenaline":
                return currentPlayer.adrenalineCount > 0;
            default:
                return false;
        }
    }

    public int GetAdrenalineCount()
    {
        return currentPlayer != null ? Mathf.Max(0, currentPlayer.adrenalineCount) : 0;
    }

    public bool ConsumeAdrenaline(int quantity = 1)
    {
        if (currentPlayer == null) return false;

        int amount = Mathf.Max(1, quantity);
        if (currentPlayer.adrenalineCount < amount) return false;

        currentPlayer.adrenalineCount -= amount;
        SavePlayerProgress();
        return true;
    }

    public void GrantStoreItem(string itemId, int quantity = 1)
    {
        if (currentPlayer == null || string.IsNullOrWhiteSpace(itemId)) return;

        string key = itemId.Trim().ToLowerInvariant();
        switch (key)
        {
            case "scanner":
                currentPlayer.hasScanner = true;
                break;
            case "lantern":
                currentPlayer.hasLantern = true;
                break;
            case "adrenaline":
                currentPlayer.adrenalineCount += Mathf.Max(1, quantity);
                break;
            default:
                Debug.LogWarning($"[AccountManager] Unknown store item '{itemId}'.");
                return;
        }

        if (currentPlayer.collectedGates != null)
        {
            string marker = $"shop_{key}";
            if (!currentPlayer.collectedGates.Contains(marker))
                currentPlayer.collectedGates.Add(marker);
        }

        SavePlayerProgress();

        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        Debug.Log($"[AccountManager] Granted store item '{itemId}' x{Mathf.Max(1, quantity)}.");
    }
}