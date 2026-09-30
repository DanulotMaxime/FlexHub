# FlexHub 1.6.0

Cette mise à jour améliore surtout le monitoring matériel, les sessions de jeu et les outils de maintenance.

## Nouveautés

- Monitoring de la puissance CPU, GPU et du total mesuré avec historique et pic maximal.
- Pics de consommation ajoutés aux sessions de jeu, aux comparaisons et à l’export CSV.
- Recherche des pilotes applicables via Windows Update pour le GPU, l’audio et le chipset.
- Nouveau module d’analyse des erreurs Windows avec explications et recommandations prudentes.

## Améliorations

- Graphiques du monitoring réorganisés, gradués et utilisables avec la molette.
- Collecte FPS PresentMon plus fiable après un redémarrage de FlexHub.
- Rapport quotidien allégé afin d’éviter les freezes passagers.
- Renommage en masse fiabilisé pour les grandes collections et les formats HEIC, WEBP, TIFF et RAW.
- Prévention des collisions de noms sans écrasement de fichier.

## Important

- FlexHub demande désormais les droits administrateur au lancement pour accéder aux capteurs matériels compatibles, notamment la puissance CPU AMD via Ryzen Master SDK.
- La puissance totale affichée correspond uniquement au CPU et au GPU mesurés. Les autres composants restent une estimation indicative séparée.
- L’audit des pilotes et l’analyse des erreurs Windows restent en lecture seule.
