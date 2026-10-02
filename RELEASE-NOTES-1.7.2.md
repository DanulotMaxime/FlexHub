# FlexHub 1.7.2

Cette mise à jour corrective rétablit le lancement automatique de FlexHub à l’ouverture de session Windows.

## Correction principale

- FlexHub ne demande plus les droits administrateur au démarrage de l’application.
- Windows peut désormais le lancer normalement depuis l’entrée « Démarrage avec Windows ».
- L’entrée de démarrage continue d’être vérifiée et recréée automatiquement lorsque l’option est activée.

## Sécurité

- Les actions réellement sensibles demandent toujours l’autorisation administrateur au moment de leur exécution.
- La purge de la mémoire en attente et les modifications du profil NVIDIA conservent leur fenêtre UAC dédiée.
- Les capteurs matériels protégés peuvent ne pas fournir certaines mesures si Windows en refuse l’accès, sans empêcher FlexHub de fonctionner.

## Installation

- L’installeur met à jour l’installation existante et conserve les paramètres personnels.
- Après installation, FlexHub réparera automatiquement son entrée de démarrage Windows si l’option est activée.
