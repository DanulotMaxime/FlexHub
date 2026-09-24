# Confidentialité — Personal Apps Hub

FlexHub fonctionne localement, à l’exception des fonctions en ligne choisies par l’utilisateur.

- Le correcteur, le traducteur et le générateur de réponse peuvent transmettre le texte sélectionné au fournisseur configuré : OpenAI, Google Gemini, Google Traduction, DeepL, LibreTranslate ou MyMemory.
- Aucun texte sélectionné n’est conservé volontairement par FlexHub.
- Les clés API sont chiffrées avec la protection de données du compte Windows (DPAPI) et restent sur l’ordinateur.
- Les journaux locaux contiennent des informations techniques et les messages d’erreur, mais pas volontairement le texte sélectionné ni les clés API.
- La vérification des mises à jour contacte l’API publique de GitHub uniquement lorsque l’utilisateur appuie sur le bouton correspondant.
- Les données locales peuvent être supprimées depuis les paramètres généraux.

## Historique du Presse-papiers Windows

L’installeur propose une option, cochée par défaut mais désactivable, pour activer l’historique du Presse-papiers Windows accessible avec `Win + V`. Cette fonction est gérée par Windows et peut conserver localement plusieurs éléments copiés. FlexHub n’active pas la synchronisation du Presse-papiers entre appareils et ne lit pas cet historique pendant l’installation.

La désinstallation de FlexHub ne désactive pas automatiquement cette préférence Windows. Elle peut être modifiée à tout moment dans **Paramètres Windows > Système > Presse-papiers**. Une stratégie d’entreprise peut empêcher son activation.

Les fournisseurs externes appliquent leurs propres conditions et politiques de confidentialité. L’utilisateur doit vérifier leurs tarifs et règles avant utilisation.

Support : Discord, pseudo **Flexron**.
