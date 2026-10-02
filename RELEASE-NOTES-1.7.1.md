# FlexHub 1.7.1

Cette mise à jour corrective fiabilise le lancement automatique de FlexHub et améliore l’organisation des sessions de jeu.

## Corrections

- FlexHub recrée automatiquement son entrée de démarrage Windows lorsque l’option est activée mais que l’entrée a disparu.
- Une erreur d’accès au registre ne bloque plus le lancement de l’application.
- Le lancement d’une version de développement ne remplace plus le chemin de l’installation officielle dans le démarrage Windows.

## Sessions de jeu

- Nouvelle section « Sessions favorites » indépendante de la limite des 10 dernières parties.
- Une session marquée d’une étoile reste visible même après de nombreuses nouvelles parties.
- Les 10 dernières sessions sont classées de la plus récente à la plus ancienne.
- Le graphique et les options sont maintenant placés au-dessus des favoris et de l’historique récent.

## Qualité

- Ajout de tests automatisés couvrant la conservation d’un favori ancien et l’ordre des sessions récentes.
