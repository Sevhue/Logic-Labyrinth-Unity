using System.Collections.Generic;

/// <summary>
/// Centralized sign-up validation for username and password.
/// Used by both UIManager (Panel 1) and CreateAccountPanel (Panel 2).
/// </summary>
public static class SignUpValidator
{
    // ── Banned / inappropriate words (case-insensitive) ──
    private static readonly HashSet<string> bannedWords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        // Filipino profanity
        "titi", "pepe", "bading", "tae", "puke", "puta", "putangina", "gago", "gaga",
        "bobo", "tanga", "ulol", "tarantado", "leche", "bwisit", "hinayupak", "hayop",
        "punyeta", "kupal", "pakyu", "tangina", "kingina", "potangina",
        "kantot", "jakol", "libog", "bilat", "bayag", "betlog",
        "pokpok", "malandi", "burat", "supot", "salsal", "kalibugan",
        "engot", "inutil", "siraulo", "unggoy", "demonyo",

        // English profanity
        "pussy", "ass", "dick", "cock", "fuck", "shit", "bitch", "bastard",
        "asshole", "damn", "crap", "penis", "vagina", "boob", "boobs",
        "nigger", "nigga", "faggot", "fag", "retard", "whore", "slut",
        "cum", "cunt", "dildo", "porn", "sex", "sexy", "nude", "naked",
        "anal", "anus", "rape", "rapist", "molest", "pedophile",
        "hentai", "xxx", "wanker", "tosser", "twat", "bollocks",
        "prick", "douche", "douchebag", "butthole", "jackass",
        "moron", "idiot", "stupid", "dumbass", "motherf",

        // Common leetspeak / evasions
        "fuk", "fck", "fuc", "sht", "btch", "dck", "azz",
        "phuck", "phuk", "biatch", "beyotch", "a$$", "d1ck",
        "sh1t", "b1tch", "p0rn", "s3x", "fvck", "p3nis",
        "pu$$y", "c0ck", "pr1ck", "n1gger", "n1gga"
    };

    /// <summary>
    /// Validates a username. Returns error message or empty string if valid.
    /// Rules: 3-20 chars, letters/numbers/_/-./ only, no profanity.
    /// </summary>
    public static string ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            return "Username cannot be empty!";

        if (username.Length < 4)
            return "Username must be at least 4 characters!";

        if (username.Length > 15)
            return "Username must be 15 characters or less!";

        // Only letters, numbers, underscore, hyphen, period
        foreach (char c in username)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                return "Username can only contain letters, numbers, _, -, or .";
        }

        // Check for profanity
        string profanityError = CheckProfanity(username);
        if (!string.IsNullOrEmpty(profanityError))
            return profanityError;

        return "";
    }

    /// <summary>
    /// Validates a password. Returns error message or empty string if valid.
    /// Rules: 8-20 chars, must have letter + number, NO special characters.
    /// </summary>
    public static string ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return "Password cannot be empty!";

        if (password.Length < 8)
            return $"Password is too short! ({password.Length}/8)\nMust be between 8 and 20 characters.";

        if (password.Length > 20)
            return $"Password is too long! ({password.Length}/20)\nMust be between 8 and 20 characters.";

        bool hasLetter = false;
        bool hasDigit = false;
        bool hasSpecial = false;

        foreach (char c in password)
        {
            if (char.IsLetter(c))
                hasLetter = true;
            else if (char.IsDigit(c))
                hasDigit = true;
            else
                hasSpecial = true;
        }

        if (hasSpecial)
            return "Password must only contain letters and numbers.\nNo special characters allowed! (e.g. !@#$%^&*)";

        if (!hasLetter && !hasDigit)
            return "Password must contain at least one letter and one number!";

        if (!hasLetter)
            return "Password must contain at least one letter!\n(Currently only numbers)";

        if (!hasDigit)
            return "Password must contain at least one number!\n(Currently only letters)";

        return "";
    }

    /// <summary>
    /// Checks if text contains any banned/profane words.
    /// Returns error message or empty string if clean.
    /// </summary>
    public static string CheckProfanity(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string lower = text.ToLower().Trim();

        // Exact match
        if (bannedWords.Contains(lower))
            return "That name contains inappropriate language.\nPlease choose a different username.";

        // Substring match — catches "xxtitixxx" etc.
        foreach (string word in bannedWords)
        {
            if (word.Length >= 3 && lower.Contains(word))
                return "That name contains inappropriate language.\nPlease choose a different username.";
        }

        return "";
    }
}
