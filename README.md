<p align="center">
  <img src="src/Overclutch.App/Assets/logo.png" alt="Overclutch" width="96">
</p>

<h1 align="center">Overclutch</h1>

<p align="center">
  Optimiseur Windows gratuit et open source pour le jeu.<br>
  <em>Free, open-source Windows optimizer for gaming (French UI for now).</em>
</p>

<p align="center">
  <a href="https://github.com/Fazycks/Overclutch/releases/latest">Télécharger</a> ·
  <a href="docs/SPEC.md">Spécification</a> ·
  <a href="LICENSE">Licence MIT</a>
</p>

---

Overclutch applique en un clic des réglages Windows et NVIDIA qui réduisent la latence et stabilisent les FPS.
Chaque réglage est expliqué, sauvegardé avant modification et annulable à tout moment. Tout est gratuit :
pas de compte, pas d'abonnement, pas de publicité.

Overclutch est une alternative indépendante, inspirée du principe d'outils comme hone.gg. Le code est
entièrement original et le projet n'est affilié à aucun éditeur.

## Fonctionnalités

- **Optimiser mon PC** : crée un point de restauration Windows, puis applique tous les réglages sûrs.
- **Tout restaurer** : remet chaque valeur modifiée à son état d'origine.
- **Réglages détaillés** : chaque optimisation s'active ou s'annule séparément, avec ce qu'elle modifie.
- **Page Avancé** : réglages débattus ou dépendants du matériel, jamais appliqués automatiquement, avec un
  avertissement sur les risques.
- **Mode jeu automatique** : détecte le jeu lancé, lui donne la priorité, passe en performances maximales et
  rétablit tout à sa fermeture.
- **Matériel et suivi en temps réel** : processeur, mémoire, carte graphique NVIDIA.
- **Maintenance** : nettoyage des fichiers temporaires et du cache DNS.
- **Mises à jour automatiques** depuis les releases GitHub.

## Réglages inclus

| Catégorie | Profil « Optimiser mon PC » | Page Avancé |
|---|---|---|
| Système et processeur | Plan d'alimentation Overclutch, priorité au premier plan, planificateur multimédia orienté jeux, Power Throttling | Core parking, dynamic tick, HPET, résolution du timer globale |
| Services et arrière-plan | Télémétrie Windows, Game DVR, suggestions et pubs, délai des apps au démarrage, transparence | Apps du Store en arrière-plan, SysMain |
| Réseau | Bridage réseau multimédia, algorithme de Nagle, Ethernet économe en énergie | Modération d'interruptions |
| GPU, jeux et souris | HAGS, Mode Jeu Windows, accélération de la souris, NVIDIA faible latence, NVIDIA filtrage de texture | NVIDIA performances maximales, Multiplane Overlays, optimisations plein écran |

Overclutch ne désactive jamais Windows Defender, Windows Update ni les protections de sécurité du processeur.

## Installation

1. Télécharge `Overclutch-win-Setup.exe` depuis la [dernière release](https://github.com/Fazycks/Overclutch/releases/latest).
2. L'installateur n'est pas signé : Windows SmartScreen affiche « Windows a protégé votre ordinateur ».
   Clique sur « Informations complémentaires », puis « Exécuter quand même ».
3. Overclutch demande les droits administrateur à chaque lancement : la plupart des réglages en ont besoin.

Configuration requise : Windows 10 ou 11, 64 bits. Les réglages NVIDIA nécessitent une carte NVIDIA et son pilote.

## Sécurité et annulation

- Avant « Optimiser mon PC », Overclutch crée un point de restauration Windows.
- La valeur d'origine de chaque élément modifié (registre, service, plan d'alimentation, configuration de
  démarrage, pilote NVIDIA) est enregistrée dans `C:\ProgramData\Overclutch\backups.json`. Réappliquer un réglage
  n'écrase jamais cette première sauvegarde.
- L'annulation restaure ces valeurs exactes, y compris en supprimant celles qui n'existaient pas au départ.

## Développement

Prérequis : [SDK .NET 10](https://dotnet.microsoft.com/download) et Windows.

```bash
git clone https://github.com/Fazycks/Overclutch.git
cd Overclutch
dotnet build Overclutch.slnx
dotnet test Overclutch.slnx
```

Lance `src/Overclutch.App` depuis Visual Studio ou Rider, ou exécute `dotnet run --project src/Overclutch.App`.
L'app se relance d'elle-même en administrateur.

| Dossier | Contenu |
|---|---|
| `src/Overclutch.Core` | Moteur de réglages, catalogue, sauvegardes, suivi, mode jeu (sans dépendance à l'interface) |
| `src/Overclutch.App` | Application WPF (WPF-UI, CommunityToolkit.Mvvm) |
| `tests/Overclutch.Core.Tests` | Tests xUnit |
| `docs/SPEC.md` | Choix techniques et architecture |

Un réglage est une liste d'opérations (valeur de registre, service, réglage d'alimentation, élément `bcdedit`,
réglage du pilote NVIDIA). Chaque opération sait dire si elle est déjà appliquée, capturer l'état d'origine et
s'appliquer. Pour ajouter un réglage, ajoute une entrée dans
[`TweakCatalog.cs`](src/Overclutch.Core/Tweaks/TweakCatalog.cs).

### Publier une version

```bash
dotnet tool restore
dotnet publish src/Overclutch.App/Overclutch.App.csproj -c Release -r win-x64 --self-contained -o publish
dotnet vpk pack --packId Overclutch --packVersion 0.1.0 --packDir publish --mainExe Overclutch.exe --packTitle Overclutch --icon src/Overclutch.App/Assets/overclutch.ico --outputDir Releases/0.1.0
```

Mets à jour `<Version>` dans `Overclutch.App.csproj`, puis publie le contenu de `Releases/<version>` dans une
release GitHub nommée `v<version>`.

## Licence

[MIT](LICENSE). Utilise Overclutch à tes risques : la plupart des réglages sont documentés et réversibles, mais
chaque PC est différent.
