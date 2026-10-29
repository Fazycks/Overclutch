using Overclutch.Core.Native;

namespace Overclutch.Core.Maintenance;

public static class MaintenanceCatalog
{
    private static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string LocalLow =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static IReadOnlyList<MaintenanceTask> All { get; } =
    [
        TempFiles(),

        WindowsUpdateCache(),

        Folders("error-reports", MaintenanceCategory.Disk, "Rapports d'erreurs",
            "Rapports de plantage d'applications en attente d'envoi ou déjà envoyés à Microsoft.",
            null,
            Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"),
            Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue"),
            Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive"),
            Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue")),

        Folders("crash-dumps", MaintenanceCategory.Disk, "Vidages mémoire",
            "Copies de la mémoire écrites lors d'un écran bleu ou d'un plantage. Elles pèsent souvent plusieurs Go.",
            "Ils servent à diagnostiquer les écrans bleus. Garde-les si tu cherches la cause d'un plantage.",
            Path.Combine(Local, "CrashDumps"),
            Path.Combine(Windows, "Minidump"),
            Path.Combine(Windows, "MEMORY.DMP"),
            Path.Combine(Windows, "LiveKernelReports")),

        new()
        {
            Id = "recycle-bin",
            Category = MaintenanceCategory.Disk,
            Title = "Corbeille",
            Description = "Vide la corbeille de tous les disques.",
            Warning = "Les fichiers sont supprimés définitivement.",
            Measure = Cleanup.RecycleBinSize,
            Run = Cleanup.EmptyRecycleBin,
        },

        Folders("shader-cache", MaintenanceCategory.Games, "Cache des shaders",
            "Shaders compilés par DirectX et le pilote NVIDIA. À vider après une mise à jour du pilote, " +
            "ou si un jeu affiche des artefacts graphiques.",
            "Les jeux recompilent leurs shaders au prochain lancement : quelques saccades sont normales au début.",
            Path.Combine(Local, "D3DSCache"),
            Path.Combine(Local, @"NVIDIA\DXCache"),
            Path.Combine(Local, @"NVIDIA\GLCache"),
            Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\DXCache"),
            Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\GLCache"),
            Path.Combine(ProgramData, @"NVIDIA Corporation\NV_Cache")),

        new()
        {
            Id = "standby-memory",
            Category = MaintenanceCategory.System,
            Title = "Mémoire en attente",
            Description = "Libère la RAM que Windows garde en cache. Utile si un jeu saccade après une longue session.",
            Measure = StandbyMemory.Size,
            Run = Cleanup.PurgeStandbyMemory,
        },

        new()
        {
            Id = "dns",
            Category = MaintenanceCategory.System,
            Title = "Cache DNS",
            Description = "Oublie les adresses mémorisées. Utile si un site ou un serveur de jeu ne répond plus.",
            Run = () => Cleanup.FlushDns() ? 0 : throw new InvalidOperationException("Windows a refusé de vider le cache DNS."),
        },
    ];

    public static MaintenanceTask? Find(string id) => All.FirstOrDefault(t => t.Id == id);

    private static MaintenanceTask TempFiles()
    {
        string[] targets = [Path.GetTempPath(), Path.Combine(Windows, "Temp")];
        // Like Disk Cleanup: recent files may still belong to an install or an app at work, even when not locked.
        var minAge = TimeSpan.FromDays(1);
        return new()
        {
            Id = "temp",
            Category = MaintenanceCategory.Disk,
            Title = "Fichiers temporaires",
            Description = "Fichiers laissés par les installations et les applications dans les dossiers Temp. " +
                          "Ceux des dernières 24 h sont gardés : une application peut encore s'en servir.",
            Measure = () => Cleanup.Measure(targets, minAge),
            Run = () => Cleanup.Clean(targets, minAge),
        };
    }

    private static MaintenanceTask WindowsUpdateCache()
    {
        string[] targets = [Path.Combine(Windows, @"SoftwareDistribution\Download")];
        return new()
        {
            Id = "windows-update",
            Category = MaintenanceCategory.Disk,
            Title = "Cache de Windows Update",
            Description = "Mises à jour déjà installées que Windows garde après les avoir téléchargées.",
            Measure = () => Cleanup.Measure(targets),
            // Windows Update holds its downloads open while it runs.
            Run = () => Cleanup.WithServiceStopped("wuauserv", () => Cleanup.Clean(targets)),
        };
    }

    private static MaintenanceTask Folders(string id, MaintenanceCategory category, string title, string description,
        string? warning, params string[] targets) => new()
    {
        Id = id,
        Category = category,
        Title = title,
        Description = description,
        Warning = warning,
        Measure = () => Cleanup.Measure(targets),
        Run = () => Cleanup.Clean(targets),
    };
}
