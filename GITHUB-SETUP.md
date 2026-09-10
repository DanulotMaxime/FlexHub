# Mise en place du dépôt GitHub

Le projet est prêt à être suivi avec Git. Aucun dépôt distant n'est encore configuré.

## 1. Installer Git

```powershell
winget install --id Git.Git -e
```

Fermer puis rouvrir le terminal après l'installation.

## 2. Créer le dépôt local

Depuis la racine du projet :

```powershell
git init
git branch -M main
git add .
git status
git commit -m "Version initiale 1.0.0"
```

Avant le commit, vérifier que `publish`, `artifacts`, les sauvegardes NVIDIA et les fichiers de clés ne figurent pas dans `git status`.

## 3. Créer le dépôt GitHub

Créer un dépôt vide, privé dans un premier temps, sans ajouter automatiquement de README, de licence ou de `.gitignore`.

Puis connecter le dépôt en remplaçant l'adresse ci-dessous :

```powershell
git remote add origin https://github.com/Flexron/NOM-DU-DEPOT.git
git push -u origin main
```

## 4. Publier une version

Une fois le dépôt relié à l'application, créer un tag déclenchera automatiquement la compilation et la création d'une GitHub Release :

```powershell
git tag v1.0.0
git push origin v1.0.0
```

L'installeur sera alors disponible dans la section **Releases** du dépôt.
