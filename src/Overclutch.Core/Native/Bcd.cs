namespace Overclutch.Core.Native;

/// <summary>Reads and writes elements of the current boot entry through bcdedit.</summary>
public static class Bcd
{
    private static string Exe => Path.Combine(Environment.SystemDirectory, "bcdedit.exe");

    /// <summary>Returns the raw value of an element, or null when it is not set.</summary>
    public static string? Read(string element)
    {
        var (code, output) = ProcessRunner.Run(Exe, "/enum {current}");
        if (code != 0)
            throw new InvalidOperationException($"bcdedit /enum a échoué : {output.Trim()}");

        // Element names are not localized, values such as Yes/Oui are.
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(element + " ", StringComparison.OrdinalIgnoreCase))
                return trimmed[element.Length..].Trim();
        }
        return null;
    }

    private static readonly string[] YesWords = ["yes", "oui", "on", "true", "1", "ja", "sí", "si", "sim"];
    private static readonly string[] NoWords = ["no", "non", "off", "false", "0", "nein", "não", "nao"];

    public static bool IsYes(string? value) =>
        value is not null && YesWords.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// bcdedit prints booleans in the display language (Oui/Non) but only accepts English ones,
    /// so a value read back must go through this before it is written again.
    /// </summary>
    public static string? ToWritable(string? value) =>
        IsYes(value) ? "yes"
        : value is not null && NoWords.Contains(value, StringComparer.OrdinalIgnoreCase) ? "no"
        : value;

    public static void Set(string element, string value) => Run($"/set {{current}} {element} {value}");

    public static void Delete(string element)
    {
        if (Read(element) is not null)
            Run($"/deletevalue {{current}} {element}");
    }

    private static void Run(string arguments)
    {
        var (code, output) = ProcessRunner.Run(Exe, arguments);
        if (code != 0)
            throw new InvalidOperationException($"bcdedit {arguments} a échoué : {output.Trim()}");
    }
}
