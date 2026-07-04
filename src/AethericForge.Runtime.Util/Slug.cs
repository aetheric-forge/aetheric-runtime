using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AethericForge.Runtime.Util;

public static partial class Slug
{
    private static readonly Regex _invalidChars = InvalidChars();
    private static readonly Regex _dashDuplicate = DashDuplicate();

    public static string From(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // normalize accents → ASCII
        string normalized = input.ToLowerInvariant()
                                 .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicode = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicode != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        string noAccents = sb.ToString().Normalize(NormalizationForm.FormC);

        // replace whitespace with dashes
        string dashed = Regex.Replace(noAccents, @"\s+", "-");

        // remove invalid chars
        string cleaned = _invalidChars.Replace(dashed, "");

        // collapse multiple dashes
        string collapsed = _dashDuplicate.Replace(cleaned, "-");

        // trim leading/trailing dashes
        return collapsed.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9-]", RegexOptions.Compiled)]
    private static partial Regex InvalidChars();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex DashDuplicate();
}
