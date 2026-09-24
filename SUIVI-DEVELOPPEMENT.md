# 🚀 FlexHub — Suivi du développement

*Dernière mise à jour : 24 septembre 2026*


## Légende

- ✅ **Terminé** — Fonction présente dans l'application.
- 🟡 **Partiel** — Une partie existe, mais la fonction décrite n'est pas complète.
- ⬜ **À faire** — Idée enregistrée, développement non commencé.
- 🔵 **À définir** — Fonctionnement ou périmètre à préciser avant développement.

Ce document est basé sur le code, le README, les notes de version 1.0.0 à 1.3.1
et la liste d'idées fournie. Les pourcentages indiquent l'état fonctionnel estimé,
pas le temps nécessaire.

> [!TIP]
> Dans VS Code, utilisez **Ctrl + Maj + V** pour ouvrir l'aperçu Markdown,
> ou **Ctrl + K**, puis **V** pour afficher le document et son aperçu côte à côte.

## Sommaire

1. [Fonctions déjà réalisées](#1-fonctions-déjà-réalisées)
2. [Fonctions partielles ou à étendre](#2-fonctions-partiellement-réalisées-ou-à-étendre)
3. [Système, nettoyage et maintenance](#3-reste-à-faire---système-nettoyage-et-maintenance)
4. [Monitoring et réseau](#4-reste-à-faire---monitoring-et-réseau)
5. [Jeux et performances](#5-reste-à-faire---jeux-et-performances)
6. [IA, texte et productivité](#6-reste-à-faire---ia-texte-et-productivité)
7. [Presse-papiers, recherche et historique](#7-reste-à-faire---presse-papiers-recherche-et-historique)
8. [Fonctions à définir](#8-fonctions-à-définir-avant-développement)
9. [Ordre de développement conseillé](#9-ordre-de-développement-conseillé)



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
- Démarrage avec Windows, thèmes clair/sombre et tailles de police.
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



## 2. FONCTIONS PARTIELLEMENT RÉALISÉES OU À ÉTENDRE


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
- Analyse prudente des lancements essentiels, facultatifs ou à vérifier, avec justification.
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


### ✅ Notification de consommation anormale CPU/RAM — Terminé
- Le monitoring surveille l’utilisation globale du CPU et de la RAM avec des seuils configurables.
- Les alertes exigent trois mesures consécutives et respectent un délai de 15 minutes.
- Les processus consommant le plus de CPU et de RAM sont affichés et ajoutés aux alertes.


### 🟡 Mise à jour automatique des applications — 25 %
- Le moteur de mise à jour sécurisé existe pour FlexHub.
- Reste à gérer une liste d'applications tierces, leurs sources et les installations nocturnes.


### 🟡 Comparaison avant/après un réglage NVIDIA — 70 %
- L'application sait appliquer, sauvegarder et restaurer un profil NVIDIA.
- Les sessions enregistrent le profil NVIDIA actif et comparent leurs mesures à une session de référence du même jeu.
- Reste à intégrer les FPS lorsqu'une source compatible sera disponible.


### 🟡 Rapport et analyse des erreurs — 15 %
- FlexHub possède son propre journal de diagnostic en langage lisible.
- Reste à lire et expliquer les événements de l'Observateur d'événements Windows.



## 3. RESTE À FAIRE - SYSTÈME, NETTOYAGE ET MAINTENANCE

- [ ] **Vider le cache RAM et purger les fichiers temporaires en un clic.** *(Nettoyage temporaire sécurisé disponible ; cache RAM à étudier.)*
- [ ] **Vider automatiquement la corbeille au-delà de X Go ou X jours.**
- [x] **Afficher l'espace disque et la santé S.M.A.R.T. des SSD.**
- [x] **Auditer les programmes lancés au démarrage et proposer leur désactivation.**
- [ ] **Détecter les logiciels inutilisés depuis une durée configurable.**
- [x] **Scanner les fichiers en double par hash avec validation avant suppression.** *(Analyse SHA-256, sélection manuelle ou automatique, conservation obligatoire d'un exemplaire et déplacement récupérable vers la Corbeille.)*
- [ ] **Vérifier les pilotes GPU, audio et chipset sur les sites fabricants.**
- [x] **Nettoyer précisément les caches de navigateurs, Steam, Visual Studio, etc.**
- [ ] **Organiser automatiquement le dossier Téléchargements selon des règles.**
- [ ] **Renommer des fichiers en masse selon leur contexte (EXIF, lieu, date, etc.).**



## 4. RESTE À FAIRE - MONITORING ET RÉSEAU

### ✅ Diagnostic réseau avancé — Terminé
- Qualité locale, Internet et jeu, détection des anomalies et diagnostic automatique.
- Le diagnostic sépare les incidents locaux, l'accès Internet et les serveurs distants, et signale lorsque les cibles nécessaires manquent.
- Historique persistant des 60 dernières mesures, conservé localement pendant 7 jours et effaçable depuis l’interface.
- Analyse ponctuelle des applications ayant du trafic actif, avec mesure de leurs destinations TCP publiques.
- L’inventaire natif Windows fournit les PID sans élévation administrateur ; le trafic UDP non mesurable est clairement signalé.

- [x] **Afficher RAM et VRAM avec un mini-graphe historique.**
- [x] **Afficher la température GPU en temps réel.** *(Température CPU retirée : capteur non fiable sur le Ryzen 7 9800X3D.)*
- [x] **Alerter lorsque le GPU dépasse un seuil de température configurable.**
- [x] **Tester le ping vers la box, Discord, Steam ou des serveurs personnalisés.**
- [x] **Mesurer en continu ping, jitter et perte de paquets vers les serveurs de jeu.**
- [x] **Détecter les connexions TCP des jeux actifs et ajouter automatiquement leurs serveurs mesurables.**
- [x] **Alerter lorsque le jitter dépasse un seuil avant une partie classée.**
- [x] **Distinguer une panne locale (Wi-Fi/LAN) d'un problème extérieur.**
- [x] **Simuler une charge CPU/GPU avec suivi de température en direct.** *(Tests CPU, GPU ou combiné séparés ; quatre niveaux de 50 à 100 %, véritable charge Direct3D 11 hors écran en 1440p/4K, historique et analyse, arrêt manuel et coupure automatique du GPU à 90 °C.)*



## 5. RESTE À FAIRE - JEUX ET PERFORMANCES

- [ ] **Estimer les FPS en arrière-plan sans overlay.**
- [x] **Détecter le jeu lancé, chronométrer la session et alerter après X heures.** *(Alerte de pause disponible mais désactivée par défaut.)*
- Le suivi des sessions reste actif en arrière-plan toutes les 15 secondes, quelle que soit la page affichée.
- [~] **Générer un rapport de session : durée, températures, FPS et chutes de FPS.** *(Durée et mesures globales du PC disponibles, avec détail séparé du processus du jeu : CPU, RAM, GPU 3D et VRAM. PresentMon/FrameView ne fournit aucune trame pour le jeu testé ; FPS à finaliser avec une source compatible.)*
- [x] **Conserver l'historique des sessions et afficher des graphes par jeu/semaine.** *(Historique local sans limite et graphique comparatif filtrable sur 7 jours, 30 jours, l'année ou toute la période.)*
- Export CSV complet avec colonnes distinctes pour les mesures globales du PC et celles du processus du jeu.
- Une session terminée peut être définie comme référence par jeu ; les sessions suivantes affichent leurs écarts CPU, RAM, GPU et température GPU.
- Le profil NVIDIA actif au démarrage est conservé avec chaque session afin d'identifier clairement les comparaisons avant/après.
- [x] **Passer automatiquement le processus d'un jeu en priorité CPU haute.** *(Option désactivée par défaut, confirmation avec aperçu des jeux concernés, priorité Temps réel interdite et restauration automatique de la priorité d’origine.)*
- [ ] **Isoler des cœurs CPU pour le jeu et déplacer les autres processus.**
- [ ] **Calculer l'eDPI et comparer la sensibilité aux joueurs professionnels.**
- [ ] **Convertir la sensibilité entre Valorant, CS2, Apex, etc., avec leur FOV.**



## 6. RESTE À FAIRE - IA, TEXTE ET PRODUCTIVITÉ

- [ ] **Détecter le ton d'un message (agressif, formel, ambigu, etc.).**
- [ ] **Analyser la légitimité d'un lien ou exécutable suspect.**
- [ ] **Ajouter des actions au menu contextuel Windows pour le texte sélectionné.**
- [ ] **Comparer deux produits/options et proposer un verdict sourcé.**
- [ ] **Ajouter des commandes vocales.**
- [ ] **Générer à 18 h un rapport des applications utilisées et fichiers modifiés.**
- [ ] **Produire des statistiques hebdomadaires d'utilisation et de productivité.**
- [ ] **Mettre en place une veille automatique sur des sources configurables.**



## 7. RESTE À FAIRE - PRESSE-PAPIERS, RECHERCHE ET HISTORIQUE

- [ ] **Reconnaître le contenu copié et proposer une action adaptée**
  (téléphone, adresse, lien YouTube, etc.).
- [ ] **Capturer l'écran périodiquement en mode opt-in, effectuer un OCR**
  et permettre une recherche dans l'historique.
- [ ] **Créer une recherche unifiée dans les fichiers, le presse-papiers,**
  les snippets et les favoris.
- [ ] **Ajouter la recherche « Où est ce fichier ? » par fragment de nom.**



## 8. FONCTIONS À DÉFINIR AVANT DÉVELOPPEMENT


### 🔵 Téléchargement intelligent / téléchargement de vidéos — À définir
- Préciser les sites autorisés, le respect des conditions d'utilisation et des droits
  d'auteur, les formats voulus et l'outil technique retenu.


### 🔵 Veille automatique — À définir
- Décider si les sources sont proposées automatiquement, renseignées par l'utilisateur,
  ou les deux. Définir aussi la fréquence, les thèmes et le format des notifications.


### 🔵 Screenshot searchable — À définir
- Définir la durée de conservation, les exclusions d'applications, le chiffrement,
  la suppression et le stockage local. Cette fonction doit rester strictement opt-in.


### 🔵 Isolation de cœurs et priorité CPU automatique — À définir
- Mesurer le bénéfice réel, prévoir une liste de jeux, un retour arrière et des garde-fous
  pour ne pas dégrader Windows ni les processus importants.


### 🔵 Analyse d'un lien ou exécutable suspect — À définir
- Définir les services externes utilisés, les données envoyées et afficher clairement
  qu'un résultat automatique ne garantit jamais qu'un fichier est sans danger.



## 9. ORDRE DE DÉVELOPPEMENT CONSEILLÉ


### Priorité 1 — améliorations rapides des fonctions existantes

## 1. Ajouter les modes « Reformule » et « Simplifie » au générateur.

## 2. Rendre la détection automatique compatible avec le fournisseur par défaut ou changer
   automatiquement de fournisseur lorsque « Détection automatique » est choisie.

## 3. Ajouter les températures, RAM/VRAM, CPU et alertes dans un tableau de monitoring.


### Priorité 2 — maintenance sûre du PC

## 4. Nettoyage des fichiers temporaires avec aperçu et confirmation.

## 5. Santé des disques et audit du démarrage en lecture seule dans un premier temps.

## 6. Monitoring réseau : ping, jitter et perte de paquets.


### Priorité 3 — suivi des jeux

## 7. Détection des sessions et durée de jeu.

## 8. Collecte des FPS/températures et rapports de session.

## 9. Historique et comparaison avant/après les réglages NVIDIA.


### Priorité 4 — fonctions sensibles ou complexes
10. Recherche unifiée, historique OCR, gestion des applications tierces et commandes vocales.
11. Actions pouvant modifier le système (suppression, désinstallation, priorité/affinité CPU)
    uniquement avec aperçu, confirmation et possibilité de retour arrière.


RÈGLE DE MISE À JOUR DE CE FICHIER
- Lorsqu'une fonction commence : remplacer [À FAIRE - 0 %] par [EN COURS - XX %].
- Lorsqu'elle est testée et utilisable : la déplacer dans « Fonctions déjà réalisées ».
- Ajouter une courte note sur ce qui reste lorsqu'une fonction est marquée [PARTIEL].
- Mettre à jour la date située en haut du document à chaque modification.
