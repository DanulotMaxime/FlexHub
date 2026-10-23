# FlexHub 1.7.5

Cette mise à jour améliore la stabilité générale et enrichit le monitoring matériel.

## Nouveautés

- Ajout de la température du processeur dans la page Monitoring et des alertes associées.
- Lecture réelle de la température et de la puissance des processeurs AMD Ryzen récents via LibreHardwareMonitor et le pilote signé PawnIO.
- Compatibilité simultanée avec l’overlay NVIDIA : FlexHub ne dépend plus de FrameView lorsque les capteurs AMD répondent.
- Autorisation administrateur demandée une seule fois au premier lancement, puis démarrage élevé via une tâche Windows limitée à l’utilisateur courant.
- FrameView reste disponible comme source de secours lorsque sa session n’est pas occupée.

## Corrections

- Correction des forts ralentissements possibles lors de la manipulation des fenêtres de l’Explorateur Windows.
- Le hook de disposition clavier n’exécute plus UI Automation dans le rappel système et ignore désormais les fenêtres du shell Windows.
- Le rapport d’activité ne relit plus les métadonnées de chaque fichier pendant les copies et déplacements importants.
- Stabilisation du rendu WPF pendant les longues sessions et les charges graphiques importantes.
- Arrêt plus propre de PresentMon et suppression des erreurs attendues lors de sa fermeture volontaire.
- Réduction des avertissements PresentMon répétés dans le journal.

## Technique

- Ajout d’un pont natif C++ reproductible pour le SDK NVIDIA FrameView.
- Ajout de diagnostics ciblés pour les capteurs CPU et de tests de contrôle supplémentaires.
- L’installeur officiel signé de PawnIO 2.2.0 est inclus et installé ou mis à niveau automatiquement lors de la première configuration administrateur.
