# 🚀 FlexHub — Suivi du développement

*Dernière mise à jour : 2 octobre 2026*


## Légende

- ✅ **Terminé** — Fonction présente dans l'application.
- 🛑 **Abandonné** — Fonction retirée de la feuille de route et non prévue.

Ce document est basé sur le code, le README, les notes de version 1.0.0 à 1.7.2
et la liste d'idées fournie. Les pourcentages indiquent l'état fonctionnel estimé,
pas le temps nécessaire.

> **État actuel :** aucune nouvelle fonction n’est planifiée. Le développement se concentre désormais sur l’optimisation, la stabilité et l’amélioration des fonctions existantes.

> [!TIP]
> Dans VS Code, utilisez **Ctrl + Maj + V** pour ouvrir l'aperçu Markdown,
> ou **Ctrl + K**, puis **V** pour afficher le document et son aperçu côte à côte.

## Sommaire

1. [Fonctions déjà réalisées](#1-fonctions-déjà-réalisées)
2. [Système et diagnostics réalisés](#2-système-et-diagnostics-réalisés)
3. [Nettoyage et maintenance réalisés](#3-nettoyage-et-maintenance-réalisés)
4. [Monitoring et réseau réalisés](#4-monitoring-et-réseau-réalisés)
5. [Jeux et performances réalisés](#5-jeux-et-performances-réalisés)
6. [IA, texte et productivité](#6-ia-texte-et-productivité)
7. [Recherche de fichiers](#7-recherche-de-fichiers)
8. [Fonctions abandonnées](#8-fonctions-abandonnées)
9. [Nouvelle orientation du développement](#9-nouvelle-orientation-du-développement)



## 1. FONCTIONS DÉJÀ RÉALISÉES


### ✅ Correcteur universel — Terminé
- Correction du texte sélectionné par Windows, OpenAI, LanguageTool local ou Gemini.
- Aperçu avant remplacement et retour possible au correcteur local.


### ✅ Traducteur universel — Terminé
- Services disponibles : DeepL, Google Traduction, Gemini, LibreTranslate et MyMemory.
- Choix des langues, raccourci global et aperçu avant remplacement.
- Traduction dans les deux sens depuis la roue d'actions.


### ✅ Détection automatique de la langue source — Terminé
- Disponible avec DeepL, Google Traduction, Gemini et LibreTranslate.
- Limitation : MyMemory exige encore une langue source explicite.


### ✅ Générateur de réponse avec choix du ton — Terminé
- Génération via Gemini ou OpenAI.
- Tons disponibles : naturel, professionnel, amical et concis.
- Instruction personnalisable et aperçu avant remplacement.


### ✅ Roue d'actions et raccourcis globaux — Terminé
- Accès rapide à la correction, la traduction et la génération de réponse.
- Raccourci configurable et aperçu intégré dans les paramètres.


### ✅ Rappel Top-Serveurs — Terminé
- Intervalle, durée d'affichage et URL configurables.
- Test manuel et notification automatique.


### ✅ Optimisation NVIDIA — Terminé
- Détection automatique des GeForce RTX séries 2000, 3000, 4000 et 5000.
- Prise en charge des variantes Ti, SUPER et Laptop.
- Profil FPS maximum, réglages DLSS compatibles, Frame Generation sur RTX 40/50
  et Multi Frame Generation sur RTX 50.
- Sauvegarde du profil courant, restauration et contrôles de compatibilité.


### ✅ Surveillance XMP de la mémoire — Terminé
- Configuration DDR4/DDR5, seuil personnalisé, contrôle périodique et alerte.
- Assistant de configuration au premier lancement.


### ✅ Azerty > Qwerty automatique — Terminé
- Passage temporaire en AZERTY dans les zones de saisie Windows.
- Mémorisation de la fenêtre QWERTY du jeu : AZERTY hors jeu, QWERTY au retour dans le jeu.
- Fin du mode chat par le raccourci, Échap, un clic ou un changement de fenêtre.


### ✅ Mise à jour automatique de FlexHub — Terminé
- Recherche des versions GitHub, téléchargement, contrôle SHA-256, installation
  silencieuse et redémarrage.
- Attention : ceci met à jour FlexHub uniquement, pas les autres applications du PC.


### ✅ Paramètres généraux et sécurité — Terminé
- Démarrage avec Windows auto-réparé lorsque l’entrée du registre disparaît, thèmes clair/sombre et tailles de police.
- Taille initiale de la fenêtre configurable : petit, moyen, grand, très grand ou plein écran.
- Centre des alertes dédié pour gérer depuis une seule fenêtre les rappels Top-Serveurs,
  les seuils CPU/RAM/GPU, les pauses de session de jeu, le jitter réseau et la surveillance XMP.
- Menu latéral organisé avec une catégorie repliable dédiée aux outils de texte.
- Tous les modules disposent d'un interrupteur et les modules désactivés sont regroupés séparément avec des compteurs par catégorie.
- Clés API enregistrées localement et chiffrées pour le compte Windows.
- Consentement explicite avant l'utilisation des services d'IA en ligne.
- Liens vers les clés API et affichage de certains quotas.


### ✅ Diagnostic et journalisation de FlexHub — Terminé
- Journal global avec rotation, erreurs, opérations et consommation mémoire du Hub.
- Notes de version intégrées et effacement des données personnelles.
- Les alertes automatiques monitoring et XMP utilisent le mode Windows sans activation et ne volent pas le focus.

### ✅ Reformulation du texte sélectionné — Terminé
- Action dédiée dans la roue avec Gemini ou OpenAI.
- Module activable indépendamment et page de réglages dédiée.
- Styles disponibles : email professionnel, message décontracté, LinkedIn enthousiaste
  ou cringe, discours médiéval, formel, amical, concis, diplomatique, humoristique,
  commercial, académique, poétique, fantasy, motivant, vieux français, français très
  complexe, scientifique, méchant, insultant, insultant discret et méchant de film.
- Conservation du sens, de la langue et des faits avec le style configuré.
- Aperçu modifiable avant le remplacement du texte.

### ✅ Simplification du texte sélectionné — Terminé
- Action dédiée dans la roue avec Gemini ou OpenAI.
- Module activable indépendamment et page de réglages dédiée.
- Trois niveaux : Simplifié, Beaucoup simplifié et Ultra simplifié.
- Réécriture dans la même langue avec des mots simples et des phrases courtes.
- Aperçu modifiable avant le remplacement du texte.

### ✅ Résumé de conversation — Terminé
- Module activable indépendamment et accessible depuis la roue d’actions.
- Prise en charge des conversations Discord, Slack, Teams et du texte libre.
- Résumé très court, court, moyen ou détaillé avec aperçu modifiable.
- Options pour conserver les participants et extraire les décisions et tâches à faire.

### ✅ Définition d’un mot — Terminé
- Module activable indépendamment et accessible depuis la roue d’actions.
- Niveaux très simple, simple, détaillé et expert.
- Utilisation du contexte de la phrase et ajout facultatif d’un exemple.
- Recherche prioritaire dans le Wiktionnaire, sans clé API.
- Secours automatique avec Gemini si le Wiktionnaire ne fournit aucun résultat exploitable.
- Affichage de la définition sans remplacer le texte sélectionné.


### ✅ Installation et publication — Terminé
- Installeur Windows x64, scripts d'installation/désinstallation et logo.
- Tests de contrôle et publication GitHub Release par tag de version.
- Option d’installation explicite, cochée par défaut, pour activer l’historique du Presse-papiers Windows (`Win + V`) sans activer sa synchronisation entre appareils.



## 2. SYSTÈME ET DIAGNOSTICS RÉALISÉS


### ✅ Nettoyage des fichiers temporaires — Terminé
- Analyse en lecture seule du dossier temporaire de l’utilisateur.
- Seuls les fichiers inutilisés depuis plus de 24 heures sont proposés.
- Aperçu du nom, de la taille, de la date et de l’emplacement avant suppression.
- Sélection explicite, confirmation obligatoire et contrôle du chemin avant suppression.
- Règles dédiées aux caches anciens de Chrome, Edge, Firefox, Discord, Steam et Visual Studio.
- La source de chaque fichier est affichée et chaque suppression est limitée à la racine autorisée analysée.
- Les fichiers temporaires marqués en lecture seule peuvent être supprimés après validation ; leur attribut est restauré si la suppression échoue.
- Option désactivable de nettoyage quotidien automatique des fichiers du dossier temporaire Windows âgés de plus de 3 jours ; les caches d'applications restent manuels.


### ✅ Santé et espace des disques — Terminé
- Affichage des volumes fixes, de leur format, de l’espace libre et du pourcentage utilisé.
- Lecture en consultation seule du type, de la taille et de l’état de santé des disques physiques
  communiqué par le service de stockage Windows.
- Affichage de la santé restante estimée depuis l’usure, de la température et des heures de
  fonctionnement lorsque le périphérique transmet ces compteurs à Windows.
- Lecture native et strictement en lecture seule de la page S.M.A.R.T./Health des disques NVMe :
  données lues/écrites, cycles, arrêts non sécurisés, erreurs média et avertissements critiques.


### ✅ Audit des programmes au démarrage — Terminé
- Inventaire en lecture seule des entrées du registre 32/64 bits et des dossiers Démarrage.
- Affichage de la commande, de la portée, de la source et de l’état communiqué par Windows.
- Analyse enrichie par familles de logiciels, éditeur et description du fichier : sécurité, pilotes, synchronisation, périphériques, accès distant, launchers, communication, création et assistants de mise à jour.
- Les programmes identifiés mais dépendants de l’usage sont séparés des entrées réellement opaques afin de limiter les verdicts « À vérifier » sans recommander de désactivation risquée.
- Activation et désactivation confirmées par l’utilisateur et réversibles depuis la même page.


### ✅ Tableau de monitoring CPU/RAM/GPU — Terminé
- Affichage en temps réel du CPU, de la RAM, du GPU NVIDIA,
  de la VRAM et de la température GPU.
- Historique graphique des 60 dernières mesures avec actualisation automatique.
- Échelle de 0 à 100 %, lignes de repère et détail CPU/RAM/GPU de chaque mesure au survol.
- Alertes configurables pour l’utilisation CPU/RAM et la température GPU, déclenchées après
  trois mesures consécutives et limitées à une notification toutes les 15 minutes.
- Panneau de réglage explicite, progression de la confirmation, bandeau d’alerte intégré
  et bouton permettant de tester immédiatement les notifications.
- Fenêtre d’alerte dédiée reprenant le style du module XMP, avec accès direct au monitoring,
  sans notification Windows superposée.
- Prise en charge des capteurs GPU NVIDIA, AMD et Intel compatibles exposés par LibreHardwareMonitor.


### ✅ Monitoring de la consommation électrique — Terminé
- Lecture prioritaire de la puissance PPT réelle du processeur AMD via l’API officielle Ryzen Master déjà installée sur le PC (mesure validée sur le Ryzen 7 9800X3D), avec secours LibreHardwareMonitor. La collecte est exécutée en arrière-plan, limitée à une mesure toutes les 15 secondes et interrompue automatiquement après 8 secondes pour éviter les blocages.
- Lecture de la puissance instantanée et de la limite du GPU NVIDIA avec `nvidia-smi`, avec secours par les capteurs matériels compatibles.
- Affichage séparé du CPU, du GPU et du total mesuré CPU + GPU.
- Conservation des 60 dernières mesures dans un graphique et suivi du pic maximal réinitialisable.
- Page de monitoring défilable à la molette, avec graphiques dimensionnés séparément, unités explicites, graduations en watts à gauche du graphique de puissance, échelle de 0 à 100 % pour l’utilisation et légendes intégrées.
- Enregistrement des pics CPU, GPU et total dans l’historique des sessions de jeu et dans l’export CSV.
- Le total mesuré CPU + GPU reste distinct de l’estimation indicative des autres composants, afin de ne pas présenter une approximation comme une mesure réelle.


### ✅ Notification de consommation anormale CPU/RAM — Terminé
- Le monitoring surveille l’utilisation globale du CPU et de la RAM avec des seuils configurables.
- Les alertes exigent trois mesures consécutives et respectent un délai de 15 minutes.
- Les processus consommant le plus de CPU et de RAM sont affichés et ajoutés aux alertes.


### ✅ Comparaison avant/après un réglage NVIDIA — Terminé
- L'application sait appliquer, sauvegarder et restaurer un profil NVIDIA.
- Les sessions enregistrent le profil NVIDIA actif et comparent leurs mesures à une session de référence du même jeu.
- Les écarts de CPU, RAM, GPU, température GPU, FPS moyens et 1 % low sont affichés lorsque les données sont disponibles.
- Les FPS moyens, les 1 % low et le temps d’image moyen sont collectés automatiquement avec PresentMon pendant les sessions compatibles.
- Les captures PresentMon orphelines laissées par un redémarrage de FlexHub sont remplacées automatiquement afin que la collecte FPS puisse reprendre sur une partie déjà lancée.


### ✅ Rapport et analyse des erreurs — Terminé
- FlexHub possède son propre journal de diagnostic en langage lisible.
- Le module « Erreurs Windows » lit en lecture seule les événements critiques et erreurs des journaux Système et Application sur les 7 derniers jours.
- Les événements répétés sont regroupés par source, identifiant et message, avec priorité, catégorie, nombre d’occurrences et dernière apparition.
- Les erreurs courantes liées aux arrêts brutaux, écrans bleus, matériel WHEA, stockage, pilote graphique ou périphérique, applications bloquées ou .NET, services, DNS, TLS, DCOM, synchronisation de l’heure, TPM et Secure Boot reçoivent une explication prudente et une action conseillée.
- Les événements DCOM 10016, très courants, sont explicitement présentés comme généralement sans conséquence afin d’éviter des modifications risquées et inutiles. Les conseils TPM rappellent de préserver la clé BitLocker avant toute intervention.
- L’analyse est lancée uniquement à la demande et plafonnée afin de ne pas créer de charge permanente.



## 3. NETTOYAGE ET MAINTENANCE RÉALISÉS

### ✅ Purge manuelle de la mémoire RAM — Terminé
- Confirmation et élévation administrateur obligatoires, avec mesure avant/après.
- Le mode automatique a été retiré car il pouvait provoquer des ralentissements.

### ✅ Gestion de la Corbeille — Terminé
- Inventaire en lecture seule, aperçu et vidage manuel confirmé.
- Contrôle quotidien facultatif désactivé par défaut, avec consentement explicite avant le vidage complet.

### ✅ Détection des logiciels et jeux inutilisés — Terminé
- Audit en lecture seule des logiciels installés, des bibliothèques Steam/Epic et de l’historique UserAssist de Windows.
- Seules les dernières exécutions connues sont signalées ; les usages inconnus restent exclus.

### ✅ Recherche des fichiers en double — Terminé
- Analyse SHA-256, sélection manuelle ou automatique et conservation obligatoire d’un exemplaire.
- Les suppressions passent par la Corbeille afin de rester récupérables.

### ✅ Vérification des pilotes — Terminé
- Inventaire local des pilotes GPU, audio et chipset avec leur version et leur date.
- Recherche des mises à jour applicables via Windows Update et accès aux supports officiels NVIDIA, AMD, Intel ou Realtek.
- Aucun téléchargement ni aucune installation automatique.

### ✅ Nettoyage des caches d’applications — Terminé
- Prise en charge des caches de navigateurs, Steam, Visual Studio et autres applications reconnues.

### ✅ Organisation du dossier Téléchargements — Terminé
- Analyse, aperçu par catégorie et déplacement confirmé sans écrasement.
- Mode quotidien facultatif et désactivé par défaut pour les fichiers reconnus âgés de plus de 24 heures.
- Les téléchargements partiels et la catégorie « Autres » restent intacts.

### ✅ Renommage intelligent de fichiers — Terminé
- Aperçu complet, sélection, préfixe, date, numérotation et génération locale d’un nom depuis le contenu ou les métadonnées.
- Prise en charge des textes, du code, des titres DOCX/PDF, des tags MP3, des métadonnées EXIF et des principaux formats d’image et RAW.
- Les collisions sont résolues sans écrasement. La fonction GPS a été retirée à la demande.



## 4. MONITORING ET RÉSEAU RÉALISÉS

### ✅ Diagnostic réseau avancé — Terminé
- Qualité locale, Internet et jeu, détection des anomalies et diagnostic automatique.
- Le diagnostic sépare les incidents locaux, l'accès Internet et les serveurs distants, et signale lorsque les cibles nécessaires manquent.
- Historique persistant des 60 dernières mesures, conservé localement pendant 7 jours et effaçable depuis l’interface.
- Analyse ponctuelle des applications ayant du trafic actif, avec mesure de leurs destinations TCP publiques.
- L’inventaire natif Windows fournit les PID sans élévation administrateur ; le trafic UDP non mesurable est clairement signalé.

### ✅ Historique RAM et VRAM — Terminé
- Affichage en temps réel avec mini-graphe historique.

### ✅ Température et alertes GPU — Terminé
- Température GPU en temps réel et seuil d’alerte configurable.
- La température CPU a été retirée, son capteur étant peu fiable sur le Ryzen 7 9800X3D.

### ✅ Mesures réseau personnalisées — Terminé
- Tests vers la box, Discord, Steam ou des serveurs personnalisés.
- Mesure continue du ping, du jitter et de la perte de paquets vers les serveurs de jeu.

### ✅ Détection réseau des jeux — Terminé
- Détection des connexions TCP des jeux actifs et ajout automatique des serveurs mesurables.
- Alerte lorsque le jitter dépasse le seuil configuré et distinction entre panne locale et problème extérieur.

### ✅ Tests de charge CPU/GPU — Terminé
- Tests CPU, GPU ou combinés avec quatre niveaux de charge de 50 à 100 %.
- Charge Direct3D 11 hors écran en 1440p/4K, historique, analyse et arrêt manuel.
- Coupure automatique du test GPU à 90 °C.



## 5. JEUX ET PERFORMANCES RÉALISÉS

### ✅ Mesure des FPS en arrière-plan — Terminé
- Collecte ciblée avec PresentMon, sans overlay ni injection dans le jeu.

### ✅ Détection et chronométrage des sessions — Terminé
- Détection du jeu actif et suivi en arrière-plan toutes les 15 secondes, quelle que soit la page affichée.
- Alerte de pause configurable et désactivée par défaut.

### ✅ Rapports et historique des sessions — Terminé
- Durée, mesures globales du PC, détail CPU/RAM/GPU/VRAM du jeu, FPS moyens, 1 % low et temps d’image moyen.
- Historique local sans limite et graphiques filtrables sur 7 jours, 30 jours, l’année ou toute la période.
- Section persistante pour les sessions favorites, indépendante des 10 dernières sessions affichées de la plus récente à la plus ancienne.
- Export CSV avec des colonnes distinctes pour le PC et le processus du jeu.

### ✅ Comparaison des performances par jeu — Terminé
- Une session terminée peut servir de référence pour comparer les sessions suivantes.
- Les écarts CPU, RAM, GPU et température GPU sont affichés et le profil NVIDIA actif est conservé.

### ✅ Priorité CPU haute pour les jeux — Terminé
- Option désactivée par défaut avec confirmation et aperçu des jeux concernés.
- La priorité Temps réel est interdite et la priorité d’origine est restaurée automatiquement.

### ✅ Calcul et comparaison de l’eDPI — Terminé
- Calcul local de l’eDPI et du cm/360 avec comparaison prudente aux médianes professionnelles de Valorant et Counter-Strike 2.

### ✅ Conversion de sensibilité entre jeux — Terminé
- Conversion avec prise en compte du FOV pour les jeux compatibles réellement détectés sur le PC.
- Lecture en consultation seule de la sensibilité, de l’ADS, du FOV et de l’accélération de WARDOGS.



## 6. IA, TEXTE ET PRODUCTIVITÉ

### ✅ Analyse locale du ton — Terminé
- Moteur explicable combinant vocabulaire, expressions, négations, contexte, ponctuation et intensité.
- Jusqu’à deux nuances sont affichées avec les indices ayant motivé le résultat.

### ✅ Analyse des liens et exécutables suspects — Terminé
- Analyse locale sans ouverture : protocole, domaine, indices d’hameçonnage, double extension, provenance Internet, signature, taille et empreinte SHA-256.
- Verdict prudent et indicateur facultatif au survol des liens accessibles par Windows.

### ✅ Rapport quotidien d’activité — Terminé
- Module local désactivé par défaut avec heure configurable.
- Échantillonnage léger de l’application active et surveillance événementielle des dossiers personnels, sans lecture du contenu.
- Sauvegarde automatique de la journée toutes les deux minutes et restauration après un redémarrage de FlexHub.
- Conservation locale longue durée des journées, des applications et des chemins des fichiers modifiés.

### ✅ Statistiques hebdomadaires d’utilisation — Terminé
- Vue sur 7 jours, applications principales, fichiers modifiés et comparaison avec les 7 jours précédents.
- Les résultats décrivent l’usage sans attribuer une note de productivité.
- Graphiques visuels par jour et par application, historique des fichiers, aperçu des images et ouverture directe du fichier ou de son emplacement.

## 7. RECHERCHE DE FICHIERS

### ✅ Recherche « Où est ce fichier ? » — Terminé
- Recherche locale annulable par fragment de nom dans les dossiers personnels, limitée à 500 résultats.
- Affichage du type, de la taille et de la date, avec ouverture du fichier ou de son emplacement.



## 8. FONCTIONS ABANDONNÉES

### 🛑 Reconnaissance du contenu copié — Abandonné
- Module retiré : utilité jugée insuffisante.

### 🛑 Recherche unifiée — Abandonnée
- La recherche dans le presse-papiers, les snippets et les favoris n’est plus prévue.
- La recherche locale « Où est ce fichier ? » reste disponible.

### 🛑 Mise à jour des applications tierces — Abandonnée
- Le moteur sécurisé de mise à jour de FlexHub reste disponible.

### 🛑 Veille automatique — Abandonnée
- Priorité donnée à l’optimisation et à l’amélioration des modules existants.

### 🛑 Isolation des cœurs CPU — Abandonnée
- Module retiré après des essais ayant provoqué des freezes et des interruptions temporaires des périphériques.

### 🛑 Menu contextuel Windows universel — Abandonné
- Windows ne fournit pas de menu contextuel universel fiable pour le texte sélectionné.
- La roue d’actions et les raccourcis couvrent déjà ce besoin.

### 🛑 Commandes vocales — Abandonnées
- Module abandonné avant développement.

### 🛑 Téléchargement intelligent ou de vidéos — Abandonné
- Fonction non prévue afin de concentrer le développement sur les outils existants.

### 🛑 Historique OCR de captures d’écran — Abandonné
- La capture périodique, l’OCR et l’indexation des captures ne sont plus prévus.


## 9. NOUVELLE ORIENTATION DU DÉVELOPPEMENT


### Priorité 1 — stabilité et performances

1. Mesurer et réduire l’utilisation CPU, RAM, disque et réseau de FlexHub en arrière-plan.
2. Supprimer les blocages de l’interface et déplacer les opérations longues hors du thread graphique.
3. Renforcer la gestion des erreurs, des annulations et des périphériques ou services indisponibles.

Première passe réalisée : analyseurs .NET sans erreur, libération de la poignée Windows utilisée par le second lancement, journalisation des configurations illisibles et des échecs d’ouverture du rappel, exclusion des fichiers de lancement local du dépôt.


### Priorité 2 — amélioration des fonctions existantes

4. Améliorer l’ergonomie, la clarté des résultats et les temps de réponse des modules actuels.
5. Étendre uniquement les formats et matériels pris en charge lorsqu’un besoin réel est constaté.
6. Corriger et enrichir les diagnostics à partir des cas réellement rencontrés.


### Priorité 3 — qualité des versions

7. Ajouter des tests de non-régression pour chaque correction importante.
8. Vérifier la consommation et la stabilité sur une utilisation prolongée avant chaque publication.
9. Maintenir la documentation, les notes de version et les garde-fous de sécurité à jour.


RÈGLE DE MISE À JOUR DE CE FICHIER
- Documenter chaque optimisation ou amélioration lorsqu’elle est testée et utilisable.
- Conserver les descriptions conformes au comportement réellement présent dans le code.
- Lorsqu’une idée est abandonnée, la déplacer dans « Fonctions abandonnées » avec une courte justification.
- Mettre à jour la date située en haut du document à chaque modification.
