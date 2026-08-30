namespace AethericForge.Runtime.Models.Identity.Directory;

internal static class DirectoryValue
{
    private const int MaxValueLength = 2048;

    public static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxValueLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be {MaxValueLength} characters or fewer.");
        }

        return normalized;
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeRequired(value, nameof(value));
}
