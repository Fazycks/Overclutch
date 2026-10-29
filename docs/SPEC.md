# Overclutch — spécification v1

Optimiseur Windows gratuit pour le jeu, inspiré du principe de hone.gg (implémentation 100 % originale).

## Décisions

| Sujet | Choix |
|---|---|
| Stack | C# / .NET 10, WPF + WPF-UI (Fluent), CommunityToolkit.Mvvm |
| UX | Bouton « Optimiser » (profil recommandé) + pages de toggles détaillés |
| Sécurité | Point de restauration Windows avant le profil + sauvegarde de chaque valeur modifiée, revert par tweak |
| Tweaks risqués | Inclus dans une page « Avancé », jamais appliqués par le profil, avec avertissement |
| GPU | NVIDIA uniquement (profil global du driver via NvAPI DRS) |
| Monitoring | Sans driver kernel : compteurs Windows + NVML (pas de température CPU) |
| Droits | Toujours administrateur : manifest `asInvoker` pour que l'installateur Velopack puisse lancer l'app, puis relance élevée au démarrage (`Program.Main`) |
| Mode jeu | Détection auto du jeu, priorité haute, plan d'alimentation max, apps de fond en priorité basse, restauration à la fermeture |
| Langue | Français uniquement |
| Distribution | GitHub Releases + mises à jour auto (Velopack) |

## Architecture

```
src/Overclutch.Core   moteur de tweaks, catalogue, sauvegardes, monitoring, mode jeu (aucune dépendance UI)
src/Overclutch.App    application WPF (vues, view-models, tray)
tests/Overclutch.Core.Tests
```

Un **tweak** = une liste d'**opérations** (valeur de registre, démarrage de service, réglage d'alimentation,
valeur bcdedit, réglage driver NVIDIA). Chaque opération sait :
- dire si elle est déjà appliquée,
- capturer l'état d'origine sous forme d'une entrée de sauvegarde autonome (sérialisée en JSON),
- s'appliquer.

Les sauvegardes sont stockées dans `%ProgramData%\Overclutch\backups.json`. La première capture d'un tweak
n'est jamais écrasée : réappliquer un tweak conserve la vraie valeur d'origine.

## Catalogue v1

Voir `src/Overclutch.Core/Tweaks/TweakCatalog.cs` — chaque entrée documente ce qu'elle modifie.

## Hors périmètre v1

Désactivation de Windows Defender / Windows Update / mitigations de sécurité (jamais), AMD GPU,
températures CPU, benchmark avant/après, comptes et abonnement.
