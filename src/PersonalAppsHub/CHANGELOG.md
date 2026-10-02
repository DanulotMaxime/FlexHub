# Notes de version

## 1.7.1

- Correction du démarrage automatique lorsque le réglage « Démarrage avec Windows » est activé mais que l’entrée correspondante a disparu du registre.
- Vérification et réparation silencieuse de l’entrée de démarrage à chaque lancement, sans empêcher FlexHub de s’ouvrir si le registre est temporairement inaccessible.
- La version de développement conserve désormais le chemin de l’installation officielle dans l’entrée de démarrage lorsqu’une installation de FlexHub est présente.
- Ajout d’une section dédiée aux sessions de jeu favorites, qui restent visibles même lorsqu’elles ne figurent plus parmi les 10 dernières sessions.
- Les 10 dernières sessions sont affichées de la plus récente à la plus ancienne.
- Réorganisation de la page Sessions de jeu : graphique et options avant les favoris et l’historique récent.
- Ajout de tests automatisés pour la conservation des favoris anciens et l’ordre des sessions récentes.

## 1.7.0

- Refonte des statistiques d’activité sur sept jours avec graphiques quotidiens, classement des applications les plus utilisées et total du temps mesuré.
- Conservation locale à long terme de l’historique d’activité, alimenté régulièrement sans lancer de scan lourd du système.
- Ajout d’une liste des fichiers récemment modifiés avec aperçu des images compatibles et accès direct au fichier ou à son dossier.
- Séparation claire entre le rapport quotidien textuel et la nouvelle fenêtre « Statistiques et historique » afin de préserver la lisibilité du rapport.
- Harmonisation visuelle des tableaux et aperçus de l’historique avec les thèmes clair et sombre de FlexHub.
- Analyse des programmes au démarrage enrichie grâce au nom, à l’éditeur, à la description et à la famille du logiciel afin de réduire les résultats « À vérifier ».
- Ajout du choix de la taille initiale de FlexHub : petit, moyen, grand, très grand ou plein écran.
- Nouveau centre des alertes regroupant les rappels Top-Serveurs, les seuils CPU/RAM/GPU, les pauses de session de jeu, le jitter réseau et la surveillance XMP.
- Synchronisation automatique des réglages du centre des alertes avec les pages d’origine et désactivation visuelle des champs non utilisés.
- Amélioration de la journalisation et sauvegarde de secours lorsque le fichier de configuration local est illisible.
- Mise à jour du suivi de développement pour refléter les fonctions réellement terminées et la nouvelle phase d’optimisation de l’application.
- Corrections de stabilité, de présentation et tests automatisés supplémentaires.

## 1.6.0

- Ajout du suivi de la consommation électrique dans le monitoring avec puissance CPU, GPU et total mesuré, historique graphique gradué en watts et pic maximal réinitialisable.
- Lecture prioritaire de la puissance PPT des processeurs AMD compatibles via Ryzen Master SDK, limitée à une interrogation toutes les 15 secondes avec délai maximal afin de préserver la réactivité du PC.
- Enregistrement des pics de puissance CPU, GPU et totale dans les sessions de jeu, les comparaisons de référence et l’export CSV.
- Correction et réorganisation de la page Monitoring : défilement à la molette, graphiques séparés et redimensionnés, unités et légendes explicites.
- Stabilisation de la collecte FPS PresentMon après un redémarrage de FlexHub grâce au remplacement des anciennes sessions de capture restées actives.
- Le rapport quotidien échantillonne désormais l’application active toutes les 30 secondes et effectue sa génération hors du fil d’interface pour réduire les freezes passagers.
- L’audit des pilotes compare maintenant l’inventaire GPU, audio et chipset aux mises à jour de pilotes réellement proposées par Windows Update, sans téléchargement ni installation automatique.
- Nouveau module « Erreurs Windows » : lecture à la demande des journaux Système et Application, regroupement des événements répétés, priorité, explication et action conseillée.
- Explications dédiées aux arrêts brutaux, écrans bleus, erreurs WHEA, stockage, pilotes graphiques et périphériques, applications bloquées, .NET, services, DNS, TLS, DCOM, TPM et Secure Boot.
- Les événements DCOM 10016 sont présentés comme généralement sans conséquence et les recommandations TPM rappellent de conserver la clé BitLocker avant toute intervention.
- Renommage en masse fiabilisé pour les grandes collections : aucun format non reconnu n’est masqué, sources de date et de nom séparées et collisions résolues dans l’aperçu sans écrasement.
- Identification explicite des images JPG, JPEG, PNG, WEBP, HEIC/HEIF, AVIF, GIF, BMP, TIFF et des principaux formats RAW.
- Suppression de la fonction GPS du renommage à la demande des utilisateurs.
- Nombreuses améliorations de robustesse et tests automatisés supplémentaires.

## 1.5.0

- Ajout d’une purge manuelle du cache RAM en attente de Windows, avec confirmation, autorisation administrateur et mesure de la mémoire disponible avant/après.
- La purge du cache RAM reste exclusivement manuelle ; le mode automatique a été retiré afin d’éviter les ralentissements et rechargements de données pendant les jeux.
- Ajout d’un module Corbeille avec inventaire, taille, ancienneté, vidage manuel confirmé et seuils automatiques facultatifs en Go ou en jours.
- Ajout d’un audit en lecture seule des logiciels et jeux Steam/Epic potentiellement inutilisés, avec seuil configurable et exclusion prudente des usages inconnus.
- Ajout d’un organisateur du dossier Téléchargements avec aperçu, sélection, classement par type et déplacement confirmé sans écrasement.
- Ajout d’un classement quotidien facultatif des téléchargements reconnus âgés de plus de 24 heures ; les fichiers partiels et la catégorie Autres restent intacts.
- Ajout du renommage de fichiers en masse avec aperçu, préfixe, cases date et numéro, sélection et confirmation.
- Ajout facultatif de noms intelligents générés localement depuis le contenu et les métadonnées des fichiers, toujours avec aperçu et confirmation.
- Ajout d’un audit en lecture seule des pilotes GPU, audio et chipset avec version, date, fichier INF et accès au support officiel du fabricant.
- Ajout d’un calculateur de sensibilité pour Valorant, CS2 et Apex Legends avec eDPI, cm/360, conversion équivalente et indication du FOV de référence.
- Le calculateur filtre désormais une bibliothèque étendue de profils et ne propose que les jeux détectés dans les installations Windows, Steam ou Epic.
- Lecture automatique des réglages de sensibilité WARDOGS, toujours sans modifier les fichiers du jeu.
- Ajout de la recherche locale de fichiers par fragment de nom, avec type, taille, date, ouverture du fichier et accès direct à son emplacement.
- Regroupement de la recherche de fichiers, du classement des téléchargements, du renommage et des doublons dans une catégorie Utilitaires dédiée.
- Ajout d’une analyse locale et explicable du ton : critique, agressif, frustré, résigné, défensif, inquiet, triste, enthousiaste, chaleureux, urgent, professionnel, incertain, directif ou neutre.
- L’analyse du ton combine familles de mots, expressions, négations, contexte, ponctuation et intensité, avec jusqu’à deux nuances et les indices ayant motivé le résultat.
- Amélioration de l’ergonomie du renommage en masse : choix du dossier plus compact, prévisualisation agrandie et champ de préfixe clarifié.
- Harmonisation de tous les tableaux : lignes, sélections, survols et en-têtes restent sombres et lisibles, y compris lorsque le contrôle perd le focus.
- Correction de plusieurs problèmes de robustesse, notamment l’encodage de l’inventaire des pilotes et la détection Unicode dans l’analyse du ton.

## 1.4.2

- Ajout du suivi des ressources utilisées directement par le processus de chaque jeu détecté.
- Les sessions en cours affichent désormais des cases distinctes pour le CPU, la RAM, le GPU 3D et la VRAM du jeu.
- Les rapports séparent clairement les performances du jeu de l’utilisation totale du PC, avec pics CPU/RAM/VRAM et moyenne/pic GPU.
- L’export CSV contient maintenant des colonnes séparées pour les mesures globales du PC et celles du processus du jeu.
- Les anciens historiques restent compatibles ; une mesure indisponible est affichée par un tiret.

## 1.4.1

- Ajout d’une option d’installation, cochée par défaut, pour activer l’historique du Presse-papiers Windows accessible avec `Win + V`.
- L’option reste désactivable pendant l’installation et n’active pas la synchronisation du Presse-papiers entre appareils.
- Mise à jour de la documentation de confidentialité pour préciser le fonctionnement et la conservation de cette préférence Windows.

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
