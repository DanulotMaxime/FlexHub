# FlexHub 1.7.3

Cette mise à jour corrige l'erreur de droits administrateur qui pouvait apparaître quelques secondes après le lancement de FlexHub.

## Monitoring matériel

- FlexHub ne lance plus AMD Ryzen Master CLI en arrière-plan.
- Le démarrage automatique reste compatible avec une session Windows standard, sans demande UAC.
- Lorsque le capteur de puissance CPU n'est pas accessible, FlexHub affiche une estimation fondée sur la charge du processeur et son enveloppe indicative.
- Les valeurs CPU et le total CPU + GPU portent la mention « estimée » ou « estimé » afin de les distinguer d'une mesure matérielle.
- La mesure de puissance GPU continue d'utiliser les données fournies par le pilote graphique.

## Installation

- L'installeur met à jour l'installation existante et conserve les paramètres personnels.
- Le lancement automatique reste activable depuis FlexHub ou pendant l'installation.
