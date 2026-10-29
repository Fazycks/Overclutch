using System.Globalization;
using Microsoft.Win32;
using NvAPIWrapper.DRS;
using Overclutch.Core.Native;

namespace Overclutch.Core.Tweaks;

public static class TweakCatalog
{
    private const string SystemProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GameConfigStore = @"System\GameConfigStore";
    private const string TcpInterfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string NetAdapterClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private static readonly Guid CoreParkingMinCores = new("0cc5b647-c1df-4637-891a-dec35c318583");

    public static IReadOnlyList<Tweak> All { get; } =
    [
        // ---------------- Système / CPU / alimentation ----------------
        new()
        {
            Id = "sys.power-plan",
            Title = "Plan d'alimentation Overclutch",
            Description = "Crée et active un plan basé sur « Performances ultimes » : le CPU ne baisse plus sa fréquence "
                          + "pour économiser l'énergie pendant que tu joues.",
            Category = TweakCategory.System,
            Operations = () => [new PowerPlanOp()],
        },
        new()
        {
            Id = "sys.foreground-priority",
            Title = "Priorité au programme au premier plan",
            Description = "Donne des tranches de temps CPU plus longues à la fenêtre active (Win32PrioritySeparation = 0x26).",
            Category = TweakCategory.System,
            Operations = () => [RegistryValueOp.Hklm(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26)],
        },
        new()
        {
            Id = "sys.mmcss-games",
            Title = "Planificateur multimédia orienté jeux",
            Description = "Augmente la priorité CPU/GPU que Windows (MMCSS) accorde aux jeux et réserve moins de CPU aux tâches de fond.",
            Category = TweakCategory.System,
            Operations = () =>
            [
                RegistryValueOp.Hklm(SystemProfile, "SystemResponsiveness", 10),
                RegistryValueOp.Hklm(SystemProfile + @"\Tasks\Games", "GPU Priority", 8),
                RegistryValueOp.Hklm(SystemProfile + @"\Tasks\Games", "Priority", 6),
                new RegistryValueOp(RegistryHive.LocalMachine, SystemProfile + @"\Tasks\Games", "Scheduling Category", "High", RegistryValueKind.String),
                new RegistryValueOp(RegistryHive.LocalMachine, SystemProfile + @"\Tasks\Games", "SFIO Priority", "High", RegistryValueKind.String),
            ],
        },
        new()
        {
            Id = "sys.power-throttling",
            Title = "Désactiver le Power Throttling",
            Description = "Empêche Windows de brider les processus qu'il juge en arrière-plan (launchers, overlays, outils de streaming).",
            Category = TweakCategory.System,
            Operations = () => [RegistryValueOp.Hklm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1)],
        },
        new()
        {
            Id = "adv.core-parking",
            Title = "Désactiver le core parking",
            Description = "Garde tous les cœurs du CPU éveillés dans le plan d'alimentation actif (cœurs minimum = 100 %).",
            Warning = "Consommation et températures au repos plus élevées. À NE PAS utiliser sur les Ryzen X3D à deux CCD "
                      + "(7900X3D, 7950X3D, 9900X3D, 9950X3D) : le driver AMD utilise le parking pour garder les jeux sur le cache 3D.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Advanced,
            Operations = () => [new PowerSettingOp(PowerPlans.ProcessorSubgroup, CoreParkingMinCores, 100)],
        },
        new()
        {
            Id = "adv.dynamic-tick",
            Title = "Désactiver le dynamic tick",
            Description = "Le timer système reste régulier au lieu de s'arrêter au repos (bcdedit disabledynamictick yes).",
            Warning = "Légère hausse de consommation. Modifie la configuration de démarrage : si BitLocker est actif, garde ta clé de récupération.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Advanced,
            RequiresReboot = true,
            Operations = () => [new BcdOp("disabledynamictick", "yes")],
        },
        new()
        {
            Id = "adv.platform-clock",
            Title = "Ne pas forcer le HPET",
            Description = "Retire « useplatformclock » si un ancien outil l'a forcé : le HPET forcé augmente la latence sur les CPU récents.",
            Warning = "Modifie la configuration de démarrage. Déjà actif sur la plupart des PC.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Advanced,
            RequiresReboot = true,
            Operations = () => [new BcdOp("useplatformclock", null)],
        },
        new()
        {
            Id = "adv.global-timer",
            Title = "Résolution du timer globale",
            Description = "Windows 11 : une demande de timer haute résolution (faite par un jeu ou un outil) s'applique à tout le système.",
            Warning = "Augmente la consommation au repos ; utile surtout avec un outil de timer resolution.",
            Category = TweakCategory.System,
            Risk = TweakRisk.Advanced,
            RequiresReboot = true,
            Operations = () => [RegistryValueOp.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1)],
        },

        // ---------------- Services & arrière-plan ----------------
        new()
        {
            Id = "bg.telemetry",
            Title = "Couper la télémétrie Windows",
            Description = "Désactive les services DiagTrack et dmwappushservice et réduit la collecte de données au minimum.",
            Category = TweakCategory.Background,
            Operations = () =>
            [
                new ServiceStartOp("DiagTrack", System.ServiceProcess.ServiceStartMode.Disabled),
                new ServiceStartOp("dmwappushservice", System.ServiceProcess.ServiceStartMode.Disabled),
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
            ],
        },
        new()
        {
            Id = "bg.game-dvr",
            Title = "Désactiver l'enregistrement en arrière-plan (Game DVR)",
            Description = "La Xbox Game Bar n'enregistre plus en continu les dernières minutes de jeu.",
            Category = TweakCategory.Background,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(GameConfigStore, "GameDVR_Enabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
            ],
        },
        new()
        {
            Id = "bg.suggestions",
            Title = "Désactiver suggestions et pubs Windows",
            Description = "Plus d'apps suggérées, d'astuces ni de contenu promotionnel dans le menu Démarrer et les notifications.",
            Category = TweakCategory.Background,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
            ],
        },
        new()
        {
            Id = "bg.startup-delay",
            Title = "Supprimer le délai des apps au démarrage",
            Description = "Windows attend normalement quelques secondes avant de lancer les apps de démarrage ; ce délai est retiré.",
            Category = TweakCategory.Background,
            Operations = () => [RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0)],
        },
        new()
        {
            Id = "bg.transparency",
            Title = "Désactiver les effets de transparence",
            Description = "Moins de travail pour le compositeur de fenêtres (DWM), surtout utile sur les GPU modestes.",
            Category = TweakCategory.Background,
            Operations = () => [RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0)],
        },
        new()
        {
            Id = "adv.background-apps",
            Title = "Bloquer les apps du Store en arrière-plan",
            Description = "Les applications du Microsoft Store ne tournent plus quand elles sont fermées.",
            Warning = "Les apps du Store (WhatsApp, Mail, Phone Link...) n'enverront plus de notifications quand elles sont fermées.",
            Category = TweakCategory.Background,
            Risk = TweakRisk.Advanced,
            Operations = () => [RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2)],
        },
        new()
        {
            Id = "adv.sysmain",
            Title = "Désactiver SysMain (Superfetch)",
            Description = "Arrête le préchargement des apps en mémoire, qui peut provoquer des pics d'utilisation disque.",
            Warning = "Sur un disque dur (HDD), l'ouverture des applications peut devenir plus lente.",
            Category = TweakCategory.Background,
            Risk = TweakRisk.Advanced,
            Operations = () => [new ServiceStartOp("SysMain", System.ServiceProcess.ServiceStartMode.Disabled)],
        },
        new()
        {
            Id = "bg.error-reporting",
            Title = "Désactiver le rapport d'erreurs Windows",
            Description = "Après un plantage, Windows n'analyse plus l'application et n'envoie plus de rapport à Microsoft (WerFault).",
            Category = TweakCategory.Background,
            Operations = () => [RegistryValueOp.Hklm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)],
        },

        // ---------------- Confidentialité & pubs ----------------
        new()
        {
            Id = "privacy.advertising-id",
            Title = "Désactiver l'identifiant publicitaire",
            Description = "Les applications ne peuvent plus utiliser ton identifiant publicitaire pour afficher des pubs ciblées.",
            Category = TweakCategory.Privacy,
            Operations = () => [RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)],
        },
        new()
        {
            Id = "privacy.tailored-experiences",
            Title = "Désactiver les expériences personnalisées",
            Description = "Microsoft n'utilise plus tes données de diagnostic pour te proposer des astuces, pubs et recommandations.",
            Category = TweakCategory.Privacy,
            Operations = () => [RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)],
        },
        new()
        {
            Id = "privacy.settings-suggestions",
            Title = "Masquer le contenu suggéré dans Paramètres",
            Description = "Plus de suggestions ni de promotions de services Microsoft dans l'application Paramètres.",
            Category = TweakCategory.Privacy,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0),
            ],
        },
        new()
        {
            Id = "privacy.web-search",
            Title = "Retirer les résultats web de la recherche",
            Description = "La recherche du menu Démarrer ne cherche plus sur Bing : seulement tes applications, fichiers et paramètres.",
            Category = TweakCategory.Privacy,
            RequiresReboot = true,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
                RegistryValueOp.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0),
            ],
        },
        new()
        {
            Id = "privacy.activity-history",
            Title = "Désactiver l'historique d'activités",
            Description = "Windows n'enregistre plus les applications et documents que tu ouvres et ne les envoie plus à Microsoft.",
            Category = TweakCategory.Privacy,
            Operations = () =>
            [
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0),
            ],
        },
        new()
        {
            Id = "privacy.office-telemetry",
            Title = "Couper la télémétrie de Microsoft Office",
            Description = "Word, Excel, Outlook... n'envoient plus de données de diagnostic à Microsoft.",
            Category = TweakCategory.Privacy,
            Operations = () => IfInstalled("winword.exe",
            [
                RegistryValueOp.Hkcu(@"Software\Policies\Microsoft\office\common\clienttelemetry", "SendTelemetry", 3),
                RegistryValueOp.Hkcu(@"Software\Policies\Microsoft\office\common\clienttelemetry", "DisableTelemetry", 1),
            ]),
        },
        new()
        {
            Id = "adv.edge-background",
            Title = "Edge : ne plus tourner en arrière-plan",
            Description = "Microsoft Edge ne se précharge plus au démarrage de Windows et se ferme vraiment quand tu fermes sa fenêtre.",
            Warning = "Edge affichera « Votre navigateur est géré par votre organisation ». Ses extensions et notifications "
                      + "ne fonctionnent plus quand il est fermé.",
            Category = TweakCategory.Privacy,
            Risk = TweakRisk.Advanced,
            Operations = () =>
            [
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0),
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0),
            ],
        },
        new()
        {
            Id = "adv.chrome-background",
            Title = "Chrome : arrière-plan et télémétrie",
            Description = "Google Chrome se ferme vraiment quand tu fermes sa fenêtre et n'envoie plus de statistiques d'utilisation.",
            Warning = "Chrome affichera « Géré par votre organisation ». Ses extensions et notifications ne fonctionnent plus quand il est fermé.",
            Category = TweakCategory.Privacy,
            Risk = TweakRisk.Advanced,
            Operations = () => IfInstalled("chrome.exe",
            [
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled", 0),
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Google\Chrome", "MetricsReportingEnabled", 0),
            ]),
        },
        new()
        {
            Id = "adv.firefox-telemetry",
            Title = "Firefox : couper la télémétrie",
            Description = "Mozilla Firefox n'envoie plus de télémétrie, et son agent « navigateur par défaut » ne renvoie plus d'informations.",
            Warning = "Firefox affichera « Votre navigateur est géré par votre organisation » dans ses paramètres.",
            Category = TweakCategory.Privacy,
            Risk = TweakRisk.Advanced,
            Operations = () => IfInstalled("firefox.exe",
            [
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Mozilla\Firefox", "DisableTelemetry", 1),
                RegistryValueOp.Hklm(@"SOFTWARE\Policies\Mozilla\Firefox", "DisableDefaultBrowserAgent", 1),
            ]),
        },

        // ---------------- Réseau ----------------
        new()
        {
            Id = "net.throttling",
            Title = "Désactiver le bridage réseau multimédia",
            Description = "Windows limite le trafic réseau pendant la lecture multimédia ; cette limite est levée (NetworkThrottlingIndex).",
            Category = TweakCategory.Network,
            Operations = () => [RegistryValueOp.Hklm(SystemProfile, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF))],
        },
        new()
        {
            Id = "net.nagle",
            Title = "Désactiver l'algorithme de Nagle",
            Description = "Les petits paquets TCP partent immédiatement au lieu d'être regroupés : moins de latence dans les jeux qui utilisent TCP.",
            Category = TweakCategory.Network,
            Operations = () => [.. ActiveTcpInterfaces().SelectMany(path => new ITweakOperation[]
            {
                RegistryValueOp.Hklm(path, "TcpAckFrequency", 1),
                RegistryValueOp.Hklm(path, "TCPNoDelay", 1),
            })],
        },
        new()
        {
            Id = "net.energy-efficient-ethernet",
            Title = "Désactiver l'Ethernet économe en énergie",
            Description = "La carte réseau ne se met plus en veille entre deux paquets (Energy Efficient Ethernet / EEE).",
            Category = TweakCategory.Network,
            RequiresReboot = true,
            Operations = () => [.. AdapterKeysWith("*EEE").Select(path =>
                new RegistryValueOp(RegistryHive.LocalMachine, path, "*EEE", "0", RegistryValueKind.String))],
        },
        new()
        {
            Id = "adv.interrupt-moderation",
            Title = "Désactiver la modération d'interruptions",
            Description = "La carte réseau signale chaque paquet immédiatement au CPU au lieu de les grouper.",
            Warning = "Augmente l'utilisation CPU sous forte charge réseau (téléchargements). Gain de latence faible et variable.",
            Category = TweakCategory.Network,
            Risk = TweakRisk.Advanced,
            RequiresReboot = true,
            Operations = () => [.. AdapterKeysWith("*InterruptModeration").Select(path =>
                new RegistryValueOp(RegistryHive.LocalMachine, path, "*InterruptModeration", "0", RegistryValueKind.String))],
        },

        // ---------------- GPU, jeux & souris ----------------
        new()
        {
            Id = "gpu.hags",
            Title = "Planification GPU accélérée (HAGS)",
            Description = "Le GPU gère lui-même sa file de travail : moins de charge CPU, requis pour la génération d'images DLSS 3.",
            Category = TweakCategory.GpuGamingInput,
            RequiresReboot = true,
            Operations = () => [RegistryValueOp.Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)],
        },
        new()
        {
            Id = "game.game-mode",
            Title = "Activer le Mode Jeu Windows",
            Description = "Windows donne la priorité au jeu et suspend Windows Update et les notifications pendant que tu joues.",
            Category = TweakCategory.GpuGamingInput,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
                RegistryValueOp.Hkcu(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1),
            ],
        },
        new()
        {
            Id = "input.mouse-acceleration",
            Title = "Désactiver l'accélération de la souris",
            Description = "Décoche « Améliorer la précision du pointeur » : un même geste donne toujours la même distance, indispensable pour viser.",
            Category = TweakCategory.GpuGamingInput,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(@"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String),
                RegistryValueOp.Hkcu(@"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String),
                RegistryValueOp.Hkcu(@"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String),
            ],
            AfterChange = ApplyMouseSettingsLive,
        },
        new()
        {
            Id = "nv.low-latency",
            Title = "NVIDIA : mode faible latence",
            Description = "Limite à 1 le nombre d'images préparées à l'avance par le CPU (Mode faible latence = Activé).",
            Category = TweakCategory.GpuGamingInput,
            Operations = () => [new NvidiaSettingOp((uint)KnownSettingId.PreRenderLimit, 1)],
        },
        new()
        {
            Id = "nv.texture-filtering",
            Title = "NVIDIA : filtrage de texture « Haute performance »",
            Description = "Réglage global du driver qui privilégie les FPS sur la qualité du filtrage, avec une différence visuelle minime.",
            Category = TweakCategory.GpuGamingInput,
            Operations = () => [new NvidiaSettingOp((uint)KnownSettingId.QualityEnhancements, 20)],
        },
        new()
        {
            Id = "adv.nv-max-performance",
            Title = "NVIDIA : performances maximales préférées",
            Description = "Le GPU reste à ses fréquences maximales au lieu d'adapter son horloge à la charge.",
            Warning = "Le GPU consomme et chauffe davantage, même sur le bureau.",
            Category = TweakCategory.GpuGamingInput,
            Risk = TweakRisk.Advanced,
            Operations = () => [new NvidiaSettingOp((uint)KnownSettingId.PreferredPerformanceState, 1)],
        },
        new()
        {
            Id = "adv.mpo",
            Title = "Désactiver les Multiplane Overlays (MPO)",
            Description = "Corrige les scintillements et saccades connus avec certains drivers NVIDIA, surtout en multi-écran.",
            Warning = "Sur un PC sans ces problèmes, peut légèrement augmenter la latence en fenêtré sans bordure.",
            Category = TweakCategory.GpuGamingInput,
            Risk = TweakRisk.Advanced,
            RequiresReboot = true,
            Operations = () => [RegistryValueOp.Hklm(@"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5)],
        },
        new()
        {
            Id = "adv.fullscreen-optimizations",
            Title = "Désactiver les optimisations plein écran",
            Description = "Les jeux en plein écran utilisent le vrai mode exclusif au lieu du mode fenêtré optimisé de Windows.",
            Warning = "Désactive Auto HDR et peut casser certains overlays. Sur Windows 11, le gain est souvent nul.",
            Category = TweakCategory.GpuGamingInput,
            Risk = TweakRisk.Advanced,
            Operations = () =>
            [
                RegistryValueOp.Hkcu(GameConfigStore, "GameDVR_FSEBehaviorMode", 2),
                RegistryValueOp.Hkcu(GameConfigStore, "GameDVR_HonorUserFSEBehaviorMode", 1),
                RegistryValueOp.Hkcu(GameConfigStore, "GameDVR_FSEBehavior", 2),
                RegistryValueOp.Hkcu(GameConfigStore, "GameDVR_DXGIHonorFSEWindowsCompatible", 1),
            ],
        },
    ];

    public static IEnumerable<Tweak> Profile => All.Where(t => t.Risk == TweakRisk.Safe);

    public static Tweak Get(string id) => All.Single(t => t.Id == id);

    /// <summary>Returns no operation (the tweak shows as unavailable) when the app is not installed.</summary>
    private static IReadOnlyList<ITweakOperation> IfInstalled(string exe, IReadOnlyList<ITweakOperation> operations) =>
        RegistryAccess.KeyExists(RegistryHive.LocalMachine, $@"{AppPathsKey}\{exe}")
        || RegistryAccess.KeyExists(RegistryHive.CurrentUser, $@"{AppPathsKey}\{exe}")
            ? operations
            : [];

    private static IEnumerable<string> ActiveTcpInterfaces() =>
        RegistryAccess.SubKeyNames(RegistryHive.LocalMachine, TcpInterfaces)
            .Select(name => $@"{TcpInterfaces}\{name}")
            .Where(path => HasAddress(path, "DhcpIPAddress") || HasAddress(path, "IPAddress"));

    private static bool HasAddress(string path, string name) => RegistryAccess.Read(RegistryHive.LocalMachine, path, name) switch
    {
        string s => s.Length > 0 && s != "0.0.0.0",
        string[] multi => multi.Any(a => a.Length > 0 && a != "0.0.0.0"),
        _ => false,
    };

    private static IEnumerable<string> AdapterKeysWith(string valueName) =>
        RegistryAccess.SubKeyNames(RegistryHive.LocalMachine, NetAdapterClass)
            .Where(name => name.All(char.IsDigit))
            .Select(name => $@"{NetAdapterClass}\{name}")
            .Where(path => RegistryAccess.Read(RegistryHive.LocalMachine, path, valueName) is not null);

    /// <summary>Registry changes to the mouse only apply at next logon unless broadcast.</summary>
    private static void ApplyMouseSettingsLive()
    {
        static int Read(string name) =>
            int.TryParse(RegistryAccess.Read(RegistryHive.CurrentUser, @"Control Panel\Mouse", name) as string,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        int[] values = [Read("MouseThreshold1"), Read("MouseThreshold2"), Read("MouseSpeed")];
        NativeMethods.SystemParametersInfo(NativeMethods.SpiSetMouse, 0, values,
            NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendChange);
    }
}
