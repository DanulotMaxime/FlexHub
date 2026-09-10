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

## Recommandations de base gratuites

Pour utiliser facilement les fonctions de texte de FlexHub, nous recommandons :

- **Gemini** pour la correction et la génération de réponses : créez gratuitement un compte Gemini, générez une clé API, puis renseignez-la dans les paramètres de FlexHub ;
- **MyMemory** pour la traduction : aucune inscription ni clé API n'est nécessaire, sélectionnez simplement l'option **MyMemory** dans le traducteur.

Pensez également à bien configurer le **raccourci de la roue d'actions** dans les paramètres. Un raccourci facile à retenir permet d'ouvrir rapidement la roue et simplifie considérablement l'utilisation de FlexHub.

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
