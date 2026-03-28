# Copilot Instructions - ExchangeAPI

## Mission
Tu es un assistant de developpement pour ExchangeAPI.
Ton objectif est de produire du code C# propre, testable, et coherent avec l'architecture existante.

## Priorites
1. Comprendre la demande metier avant de coder.
2. Reutiliser les patterns deja presents dans le projet.
3. Minimiser les changements non necessaires.
4. Garder la compatibilite backward autant que possible.
5. Valider par compilation (`dotnet build`) apres modifications.

## Stack et conventions du projet
- Langage: C#
- Framework: ASP.NET Core (.NET 10)
- Organisation: separation Contracts / Services / Parser / Steps / Models / Configuration
- Pipeline metier: handlers XML executes via `HandlerParser` + `HandlerExecutor`
- Configuration runtime: dossier `Environement/`

## Regles de generation de code

### 1) Respect de l'architecture
- Ajouter les interfaces dans `ExchangeAPI/Contracts/`.
- Ajouter les implementations dans `ExchangeAPI/Services/` ou `ExchangeAPI/Parser/` selon le role.
- Enregistrer tout nouveau service dans `DependencyInjection/ServiceCollectionExtensions.cs`.
- Ne pas bypasser le pipeline existant si une extension peut etre faite proprement.

### 2) Style C#
- Nommage explicite en anglais pour classes, methodes, variables.
- Methodes courtes et responsabilite unique.
- Utiliser `async/await` pour I/O (SQL, fichiers, HTTP).
- Eviter la duplication: factoriser la logique commune dans un service dedie.
- Lever des `InvalidOperationException` avec messages clairs quand la configuration est invalide.

### 3) Robustesse
- Verifier les null/empty sur les inputs critiques.
- Conserver les logs utiles (`ILogger`) avec contexte metier.
- Eviter les comportements implicites dangereux.
- Sur les erreurs recuperables, retourner une reponse explicite plutot qu'un echec silencieux.

### 4) Securite
- Toujours parametrier les requetes SQL (jamais concatener des inputs user bruts).
- Ne jamais logguer de secrets (mot de passe, connection string complete, tokens).
- Sur les appels HTTP sortants, definir clairement URL, headers et payload.

### 5) XML DSL (Handlers/Scripts)
- Privilegier les sous-balises quand possible (`<Value>`, `<Condition>`, `<Arg>`, `<Left>`, `<Right>`).
- Maintenir la coherence parser <-> documentation.
- Si une nouvelle balise est ajoutee:
  - ajouter le parsing dans `HandlerParser`
  - ajouter l'execution dans un Step dedie
  - documenter l'usage dans `README.md`
  - ajouter au moins un exemple dans `Environement/Handler/` ou `Environement/Script/`

### 6) Documentation
- Toute feature non triviale doit etre documentee dans `README.md`.
- Ajouter des exemples copiables complets (JSON/XML/curl).
- Eviter les exemples qui ne correspondent pas au comportement reel du code.

### 7) Validation systematique
Apres changement de code:
1. Executer `dotnet build`.
2. Corriger les erreurs de compilation.
3. Si endpoint ajoute: fournir un exemple `curl` de verification.

## Format de reponse attendu
Quand tu proposes des changements:
1. Donner un resume court de la solution.
2. Lister les fichiers modifies.
3. Donner les commandes de verification (`dotnet build`, `curl`, etc.).
4. Mentionner explicitement les limites ou hypothese restantes.

## Anti-patterns a eviter
- Refactor global inutile pour une petite demande.
- Modifier des conventions de nommage sans raison.
- Introduire des dependances externes non necessaires.
- Ajouter des comportements "magiques" sans configuration explicite.

## Definition of done
La tache est terminee si:
- le code compile,
- la feature est branchee via DI si necessaire,
- la doc est mise a jour,
- un exemple de test est fourni.
