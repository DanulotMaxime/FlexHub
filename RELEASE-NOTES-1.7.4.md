# FlexHub 1.7.4

Cette mise à jour corrective restaure les statistiques FPS des sessions de jeu et élimine plusieurs erreurs intermittentes relevées dans le journal de diagnostic.

## Statistiques des jeux

- Les FPS moyens, le 1 % low et le temps d’image sont de nouveau collectés et enregistrés dans l’historique.
- FlexHub utilise désormais une seule session PresentMon partagée entre les jeux au lieu d’en ouvrir une par processus.
- Cette nouvelle collecte évite l’épuisement des ressources ETW de Windows et les échecs PresentMon portant le code 1450.
- La mesure redémarre automatiquement après une interruption et s’arrête proprement lorsqu’aucun jeu n’est actif.

## Stabilité

- Le détecteur de liens ignore proprement les refus d’accès et délais d’attente temporaires de Windows UI Automation.
- Le renommage en masse ne lit plus les contrôles de l’interface depuis un thread d’arrière-plan.
- Une erreur de renommage ne remonte plus jusqu’au gestionnaire global WPF et l’action reste réutilisable.
- La fermeture de FlexHub protège désormais la libération du verrou d’instance contre une erreur Windows inattendue.

## Installation

- L’installeur met à jour l’installation existante et conserve les paramètres personnels ainsi que l’historique des sessions.
- Un redémarrage de FlexHub est nécessaire pour activer la nouvelle collecte FPS.
