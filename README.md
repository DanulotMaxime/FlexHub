# Hub personnel d'applications

## Créer l'installeur Windows

L'installeur produit une version autonome 64 bits : .NET 8 n'est donc pas requis sur le PC de destination. Il ajoute l'application à la liste des programmes Windows, fournit une désinstallation propre et propose les raccourcis ainsi que le démarrage automatique.

Prérequis de compilation : [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) et [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
winget install --id JRSoftware.InnoSetup -e
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```

Le fichier distribuable est créé dans `artifacts\installer\FlexHub-Setup-1.0.0-x64.exe`. Pour publier une autre version, utilisez par exemple `-Version 1.1.0`.

Le numéro suit le format `MAJEURE.MINEURE.CORRECTIF`. La version du programme et celle de l’installeur sont synchronisées par `Build-Installer.ps1`. Après configuration du dépôt GitHub dans la page **Version et mises à jour**, l’application peut vérifier la dernière Release publique.

La création d’un tag Git déclenche également le workflow de publication :

```powershell
git tag v1.1.0
git push origin v1.1.0
```

## Nouvelle version C#

Le Hub est maintenant développé en C# avec .NET 8 et WPF. Il contient le rappel Top-Serveurs et un correcteur universel déclenché par raccourci global. Le correcteur fonctionne localement par défaut et peut utiliser OpenAI avec une clé personnelle chiffrée par Windows.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-CSharpHub.ps1
```

Ajoutez `-SelfContained` pour produire une version autonome destinée à une machine ne possédant pas .NET 8. Le correcteur utilise par défaut `Ctrl + Alt + F8` : sélectionnez un texte, utilisez le raccourci, vérifiez l'aperçu puis acceptez le remplacement.

La clé OpenAI est facultative et n'est jamais enregistrée dans le fichier de configuration.

## Ancienne version PowerShell

Le hub reste accessible dans la zone de notification Windows (les icônes cachées). Un double-clic sur son icône ouvre une petite interface qui permet d'activer ou désactiver les outils et d'en modifier la configuration.

## Installer le hub

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-AppsHub.ps1
```

Le hub démarre immédiatement, puis automatiquement à chaque ouverture de session. Fermer sa fenêtre le réduit dans la zone de notification ; utilisez **Quitter** dans le menu de l'icône pour l'arrêter complètement.

## Première application : rappel Top-Serveurs

Ce petit outil Windows affiche un rappel toutes les **2 h 10** sous la forme d'une notification en bas à droite de l'écran. Elle ne prend pas le focus, disparaît après 20 secondes et permet d'ouvrir la page du serveur dans votre navigateur par défaut afin que vous effectuiez le vote manuellement.

Il ne clique pas sur le bouton de vote et ne contourne aucun CAPTCHA ni mécanisme anti-abus.

## Installation

Dans PowerShell, depuis ce dossier :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-VoteReminder.ps1
```

Le premier rappel apparaît environ une minute après l'installation. Les suivants apparaissent toutes les 130 minutes lorsque votre session Windows est ouverte. Si l'ordinateur est éteint à l'heure prévue, l'option `StartWhenAvailable` permet de lancer le rappel après sa remise en route.

## Test immédiat

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\VoteReminder.ps1
```

## Désinstallation

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-VoteReminder.ps1
```
