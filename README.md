# FlexHub

FlexHub est une application Windows qui réunit plusieurs outils pratiques dans une seule interface.

## Télécharger FlexHub

### [Télécharger l'installeur FlexHub](https://github.com/DanulotMaxime/FlexHub/releases/latest/download/FlexHub-Setup-x64.exe)

Téléchargez uniquement le fichier **FlexHub-Setup-x64.exe**, puis ouvrez-le pour installer l'application. Aucun autre fichier n'est nécessaire.

## À quoi sert FlexHub ?

FlexHub permet notamment de :

- corriger, traduire et générer du texte ;
- utiliser une roue d'actions avec des raccourcis ;
- recevoir un rappel Top-Serveurs ;
- appliquer des profils d'optimisation NVIDIA ;
- surveiller la configuration XMP de la mémoire ;
- recevoir les nouvelles versions depuis GitHub.

## Installation

1. Téléchargez l'installeur avec le bouton ci-dessus.
2. Ouvrez le fichier téléchargé.
3. Suivez les étapes affichées par l'installeur.
4. Lancez FlexHub depuis le menu Démarrer ou son raccourci.

FlexHub contient les composants .NET nécessaires et fonctionne sur Windows 64 bits.

## Confidentialité

Les réglages et les clés API sont enregistrés localement sur l'ordinateur. Les clés API personnelles ne sont pas incluses dans le projet GitHub.

## Support

Retrouvez-moi sur Discord : **Flexron**.

Vous pouvez également [soutenir FlexHub sur Tipeee](https://fr.tipeee.com/flexhub-applications-by-flexron/).

## Pour les développeurs

La compilation nécessite le SDK .NET 8 et Inno Setup 6 :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```

Un tag Git au format `v1.2.3` déclenche automatiquement les tests et la publication d'une nouvelle GitHub Release.
