using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Overclutch.Core.GameMode;

namespace Overclutch.Core.Profiles;

/// <summary>
/// Turns a profile into text to paste in a chat: compressed JSON in URL-safe Base64 behind a version prefix.
/// No server is involved, the code is the whole profile.
/// </summary>
public static partial class ShareCode
{
    private const string Prefix = "OC1-";
    private const int MaxNameLength = 40;
    private const int MaxEntryLength = 64;
    private const int MaxEntries = 256;
    private const int MaxCodeLength = 16 * 1024;
    private const int MaxJsonBytes = 64 * 1024;

    private const string Invalid = "Ce code n'est pas un code de profil Overclutch valide.";

    public static string Encode(Profile profile)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(Clean(profile));
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize))
            deflate.Write(json);
        return Prefix + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <exception cref="FormatException">With a message to show as is.</exception>
    public static Profile Decode(string code)
    {
        // Chats wrap long lines: spaces and line breaks are not part of the code.
        var text = string.Concat(code.Where(c => !char.IsWhiteSpace(c)));
        if (!text.StartsWith(Prefix, StringComparison.Ordinal) && VersionPrefix().IsMatch(text))
            throw new FormatException("Ce code vient d'une version plus récente d'Overclutch : mets l'application à jour.");
        if (!text.StartsWith(Prefix, StringComparison.Ordinal) || text.Length > MaxCodeLength)
            throw new FormatException(Invalid);

        try
        {
            var base64 = text[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            var compressed = Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
            var profile = JsonSerializer.Deserialize<Profile>(Inflate(compressed)) ?? throw new FormatException(Invalid);
            return Clean(profile);
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidDataException)
        {
            throw new FormatException(Invalid, e);
        }
    }

    [GeneratedRegex(@"^OC\d+-")]
    private static partial Regex VersionPrefix();

    private static byte[] Inflate(byte[] compressed)
    {
        using var deflate = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (deflate.Read(buffer) is var read and > 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > MaxJsonBytes)
                throw new InvalidDataException();
        }
        return output.ToArray();
    }

    /// <summary>What comes out of a code is someone else's text: keep it to names of the expected shape.</summary>
    private static Profile Clean(Profile profile) => profile with
    {
        Name = CleanName(profile.Name),
        Tweaks = [.. (profile.Tweaks ?? []).Where(id => id is { Length: > 0 and <= MaxEntryLength }).Distinct().Take(MaxEntries)],
        GameModeLevel = Enum.IsDefined(profile.GameModeLevel) ? profile.GameModeLevel : GameModeLevel.Balanced,
        GameModeCustom = profile.GameModeCustom ?? GameModeOptions.Balanced,
        ExtraGames = CleanExecutables(profile.ExtraGames),
        BackgroundApps = CleanExecutables(profile.BackgroundApps),
    };

    private static string CleanName(string? name)
    {
        var text = string.Concat((name ?? "").Where(c => !char.IsControl(c))).Trim();
        return text.Length == 0 ? "Profil importé" : text[..Math.Min(text.Length, MaxNameLength)].TrimEnd();
    }

    private static List<string> CleanExecutables(List<string>? names) =>
    [
        .. (names ?? [])
            .Where(n => n is { Length: > 0 and <= MaxEntryLength } && n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .Select(GameDetector.TrimExe)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntries),
    ];
}
