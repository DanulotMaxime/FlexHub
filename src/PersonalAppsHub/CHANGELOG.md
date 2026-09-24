# Notes de version

## 1.4.0

- Ajout d’un tableau de monitoring CPU, RAM, GPU, VRAM et température GPU avec historique graphique et alertes configurables.
- Nouveau diagnostic réseau avancé : ping, jitter, pertes, distinction réseau local/Internet/serveur distant, détection des serveurs de jeu et analyse ponctuelle par application.
- Conservation locale des 60 dernières mesures réseau pendant sept jours.
- Nouveau test de charge CPU et GPU avec modes séparés ou combinés, quatre niveaux de 50 à 100 %, véritable charge Direct3D 11, arrêt thermique, historique et analyse automatique.
- Détection automatique des sessions de jeu, durée, alertes facultatives, historique illimité, statistiques par période et export CSV.
- Rapports de session enrichis avec pics CPU/RAM/GPU, température, profil NVIDIA et comparaison avec une session de référence.
- Nouveau mode performance facultatif appliquant la priorité CPU Haute aux jeux détectés et restaurant automatiquement leur priorité d’origine.
- Ajout des modules de reformulation, simplification, résumé de conversation et définition d’un mot dans la roue d’actions.
- Ajout du nettoyage sécurisé des fichiers temporaires et caches d’applications avec aperçu, confirmation et nettoyage quotidien facultatif.
- Ajout de la recherche de fichiers en double par empreinte SHA-256 avec conservation obligatoire d’un exemplaire et déplacement vers la Corbeille.
- Ajout de la santé des disques, des informations NVMe S.M.A.R.T. et de l’audit réversible des programmes au démarrage.
- Réorganisation du menu par catégories, compteurs de modules et regroupement des fonctions désactivées.
- Nouveau style de cases à cocher cohérent avec les thèmes de FlexHub.
- Les alertes de pause des sessions de jeu sont désormais désactivées par défaut.
- Nombreuses améliorations de lisibilité, de sécurité, de journalisation et de stabilité.

## 1.3.1

- Ajout d’une étape de configuration de la clé API Google Gemini au premier lancement, juste après le choix de la mémoire.
- Ajout d’un lien direct vers Google AI Studio et stockage local chiffré de la clé Gemini.
- MyMemory est désormais le service de traduction présélectionné, de l’anglais vers le français.
- Le module « Clavier de saisie » est renommé « Azerty>Querty auto ».
- Le module « Azerty>Querty auto » et le rappel Top-Serveurs sont désormais désactivés par défaut sur les nouvelles installations.

## 1.3.0

- Refonte du module NVIDIA avec détection automatique des GeForce RTX séries 2000, 3000, 4000 et 5000.
- Prise en charge des variantes Ti, SUPER et Laptop avec génération d’un fichier d’optimisation propre au modèle détecté.
- Nouveau profil « FPS maximum » : performances maximales, faible latence Ultra, FPS illimités, V-Sync désactivée, cache des shaders illimité et filtrage haute performance.
- Activation des overrides DLSS Super Resolution en mode Performance pour toutes les RTX compatibles.
- Activation de Frame Generation sur RTX 40/50 et de Multi Frame Generation maximal sur RTX 50 dans les jeux compatibles.
- La version du pilote NVIDIA reste informative et ne bloque plus l’application du profil.
- Ajout de contrôles automatiques empêchant l’injection de fonctions incompatibles sur les anciennes générations.

## 1.2.1

- Installation automatique des nouvelles versions dès leur détection.
- Vérification obligatoire de l’empreinte SHA-256 de l’installeur avant son lancement silencieux.
- Le bouton de vérification manuelle utilise désormais le même processus sécurisé d’installation automatique.
- Journalisation du téléchargement, de la validation et des éventuels échecs de mise à jour.

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
