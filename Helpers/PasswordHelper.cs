namespace BiketaBai.Helpers;

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public static bool IsPasswordStrong(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);

        return hasUpper && hasLower && hasDigit;
    }

    /// <summary>
    /// Validates medium password requirements: minimum 6 characters
    /// </summary>
    public static bool IsPasswordMedium(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        return password.Length >= 6;
    }
}

