# Notes de version

## 1.2.0

- Nouveau module « Clavier de saisie automatique » pour passer temporairement du QWERTY à l’AZERTY dans les champs de texte Windows.
- Détection des zones de saisie classiques, des éditeurs, des documents, des applications WPF et de nombreuses zones de navigateur.
- Restauration automatique de la disposition précédente lorsque la zone de saisie ou la fenêtre perd le focus.
- Ajout d’un raccourci configurable pour les chats en jeu : une pression active l’AZERTY et la suivante restaure le QWERTY sans bloquer la touche envoyée au jeu.
- Ajout d’une page dédiée au module avec activation, configuration, test intégré et état de fonctionnement.
- Nouveau journal global FlexHub accessible depuis les paramètres généraux, avec diagnostic des erreurs, de la mémoire, des handles, des threads et du ramasse-miettes .NET.
- Rotation automatique du journal, migration de l’ancien historique et surveillance des hausses anormales de mémoire.
- Réduction des interrogations d’accessibilité en arrière-plan afin de limiter l’utilisation des ressources.

## 1.1.0

- Ajout de liens directs pour obtenir les clés API depuis les paramètres généraux.
- Ajout d’un suivi des quotas : caractères restants pour DeepL, dernière limite connue pour OpenAI et accès direct aux tableaux de bord Google.
- Nouvelle roue d’actions agrandie avec des boutons carrés et des pictogrammes plus lisibles.
- La zone de traduction de la roue permet maintenant de traduire dans les deux sens, sans clic, selon les langues configurées.
- Mise à jour de l’aperçu de la roue dans l’application avec redimensionnement automatique.
- Intégration du logo FlexHub dans l’exécutable, l’installeur et les raccourcis Windows.

## 1.0.1

- Vérification automatique des nouvelles versions au lancement.
- Fenêtre de confirmation avant le téléchargement et l’installation.
- Vérification SHA-256 de l’installeur téléchargé depuis GitHub.
- Installation silencieuse puis redémarrage automatique de FlexHub.

## 1.0.0

- Première version publique de FlexHub.
- Correcteur, traducteur et générateur de réponse universels.
- Rappel Top-Serveurs et roue d’actions.
- Profils NVIDIA avec sauvegarde préalable.
- Surveillance mémoire configurable pour DDR4 et DDR5.

Les prochaines notes peuvent être ajoutées en tête de ce fichier lors de chaque nouvelle version.
