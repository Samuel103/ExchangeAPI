# ExchangeAPI

Un serveur HTTP dynamique piloté par configuration. Les endpoints, les sources de données et la logique de traitement sont définis entièrement **hors du code**, dans des fichiers JSON et XML. Aucune recompilation n'est nécessaire pour ajouter ou modifier un endpoint.

---

## Contexte

ExchangeAPI est conçu pour servir d'intermédiaire configurable entre des clients HTTP et des sources de données (bases SQL, fichiers). L'objectif est de décrire le comportement d'un endpoint directement dans un fichier XML — un **Handler** — sans avoir à écrire de code C# à chaque fois. L'application lit ces fichiers au démarrage, enregistre les routes dynamiquement, et exécute la pipeline de steps à chaque requête.

---

## Structure du projet

```
ExchangeAPI/
├── ExchangeAPI/                  # Application ASP.NET Core (.NET 10)
│   ├── Program.cs
│   ├── appsettings.json          # Configuration runtime (chemins)
│   ├── Configuration/            # Options typées
│   ├── Contracts/                # Interfaces (DI)
│   ├── Core/                     # DynamicApiServer, EndpointParser
│   ├── DependencyInjection/      # Enregistrement des services
│   ├── Models/                   # DynamicEndpoint, HandlerResponse
│   ├── Parser/
│   │   ├── Execution/            # HandlerExecutionContext, VariableInterpolator
│   │   ├── Executor/             # HandlerExecutor
│   │   ├── Interface/            # IHandlerStep
│   │   ├── Parser/               # HandlerParser (XML → steps)
│   │   └── Steps/                # LogStep, SetStep, SqlQueryStep, FileReadStep, ReturnStep
│   └── Services/                 # SourceRegistry
│
└── Environement/                 # Fichiers de configuration runtime (hors code)
    ├── endpoints.json            # Déclaration des routes HTTP
    ├── configurations.json       # Sources de données (DB, fichiers)
    ├── Environement.xsd          # Schéma XML de validation des handlers
    └── Handler/                  # Un fichier XML par handler
        └── Test.xml
```

---

## Configuration

### `appsettings.json`

Pointe vers le dossier d'environnement et les fichiers de configuration.

```json
{
  "WorkingFolder": "/chemin/vers/Environement/",
  "EndpointsFile": "endpoints.json",
  "ScriptDataFile": "configurations.json"
}
```

| Clé | Description |
|-----|-------------|
| `WorkingFolder` | Chemin absolu (ou relatif) vers le dossier contenant `endpoints.json`, `configurations.json` et le sous-dossier `Handler/` |
| `EndpointsFile` | Nom du fichier de déclaration des routes (défaut : `endpoints.json`) |
| `ScriptDataFile` | Nom du fichier de sources de données (défaut : `configurations.json`) |

---

### `endpoints.json`

Déclare les routes HTTP exposées par le serveur. Chaque entrée associe une URL et une méthode HTTP à un fichier handler XML.

```json
[
  {
    "path": "/hello",
    "method": "GET",
    "handler": "Test"
  }
]
```

| Champ | Description |
|-------|-------------|
| `path` | Chemin de la route (ex. `/users`, `/orders/{id}`) |
| `method` | Méthode HTTP : `GET`, `POST`, `PUT`, `DELETE`, etc. |
| `handler` | Nom du fichier XML dans `Handler/` (sans l'extension `.xml`) |

---

### `configurations.json`

Déclare les sources de données nommées. Ces noms sont utilisés comme valeur de l'attribut `Source` dans les steps XML.

```json
{
  "Sources": {
    "MyDB1": {
      "connectionString": "Server=localhost,1433;Database=EXCHANGE;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True;"
    },
    "MyFile1": {
      "path": "../Inbound.csv"
    }
  }
}
```

Chaque source est soit une **base de données SQL** (champ `connectionString`), soit un **fichier** (champ `path`). Le chemin d'un fichier peut être absolu ou relatif au `WorkingFolder`.

---

## Handlers XML

Un handler est un fichier XML situé dans `Handler/`. Il décrit une pipeline de steps exécutés séquentiellement à chaque requête. Le schéma est validé par `Environement.xsd`.

```xml
<Handler
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="../Environement.xsd">

  <!-- steps ici -->

</Handler>
```

---

## Steps disponibles

### `Set` — Assigner une variable

Crée ou écrase une variable dans le contexte d'exécution. La valeur peut être statique ou contenir des références à d'autres variables via `{{varName}}`.

```xml
<Set Var="userId" Value="42" />
<Set Var="greeting" Value="Bonjour {{userId}}" />
```

| Attribut | Requis | Description |
|----------|--------|-------------|
| `Var` | ✅ | Nom de la variable à créer |
| `Value` | ✅ | Valeur à assigner (supporte `{{varName}}`) |

---

### `Log` — Écrire dans les logs

Affiche un message dans la sortie du serveur. Utile pour le débogage d'une pipeline.

```xml
<Log Message="Traitement pour userId={{userId}}" />
```

| Attribut | Requis | Description |
|----------|--------|-------------|
| `Message` | ✅ | Message à afficher (supporte `{{varName}}`) |

---

### `SqlQuery` — Exécuter une requête SQL

Exécute une requête SELECT sur une source SQL nommée et stocke la liste de résultats (tableau de lignes) dans une variable.

Les références `{{varName}}` dans la requête sont automatiquement converties en **paramètres SQL** (`@varName`) — aucune injection SQL n'est possible.

```xml
<SqlQuery
  Source="MyDB1"
  Query="SELECT Id, Name FROM Users WHERE Id = {{userId}}"
  Var="users" />
```

| Attribut | Requis | Description |
|----------|--------|-------------|
| `Source` | ✅ | Nom de la source dans `configurations.json` |
| `Query` | ✅ | Requête SQL (supporte `{{varName}}` → paramétré) |
| `Var` | ✅ | Variable qui recevra la liste de lignes |

**Format du résultat** : liste de dictionnaires `[{ "Id": 1, "Name": "Alice" }, ...]`

---

### `FileRead` — Lire un fichier

Lit le contenu complet d'un fichier déclaré dans `configurations.json` et le stocke dans une variable.

```xml
<FileRead Source="MyFile1" Var="fileContent" />
```

| Attribut | Requis | Description |
|----------|--------|-------------|
| `Source` | ✅ | Nom de la source fichier dans `configurations.json` |
| `Var` | ✅ | Variable qui recevra le contenu texte du fichier |

---

### `Return` — Retourner la réponse HTTP

Définit la réponse HTTP retournée au client et **arrête immédiatement** l'exécution des steps suivants.

`Data` peut être :
- du **JSON inline** : `Data='{"message":"ok"}'`
- une **référence à une variable** : `Data="{{users}}"` — si la variable contient un objet ou une liste, elle est sérialisée directement
- absent : retourne uniquement le code HTTP sans corps

```xml
<!-- Retourner une variable SQL -->
<Return Status="200" Data="{{users}}" />

<!-- Retourner du JSON statique -->
<Return Status="201" Data='{"created":true}' />

<!-- Retourner un code sans corps -->
<Return Status="204" />
```

| Attribut | Requis | Description |
|----------|--------|-------------|
| `Status` | ❌ | Code HTTP (défaut : `200`) |
| `Data` | ❌ | Corps de la réponse JSON, ou `{{varName}}` |

---

## Interpolation de variables `{{varName}}`

La syntaxe `{{varName}}` est disponible dans les attributs `Message`, `Value`, `Query` et `Data`.

**Comportement :**
- Si le template est exactement `{{varName}}` (et rien d'autre), la valeur de la variable est retournée **telle quelle** — utile pour passer un objet complet (ex. liste SQL) dans `Return`.
- Sinon, chaque occurrence est remplacée par la représentation texte de la valeur.
- Dans `SqlQuery`, les occurrences sont remplacées par des **paramètres SQL nommés** pour éviter toute injection.

---

## DSL avancée (nouveautés)

Cette version ajoute des balises de contrôle de flux, des appels HTTP externes et des fonctions utilitaires orientées sous-balises (pas uniquement des attributs).

### 1) Contrôle de flux

#### `If` / `Else`

```xml
<If>
  <Condition>
    <GreaterThan>
      <Left><Get Name="total" /></Left>
      <Right>100</Right>
    </GreaterThan>
  </Condition>
  <Then>
    <Set Name="segment" Value="VIP" />
  </Then>
  <Else>
    <Set Name="segment" Value="STANDARD" />
  </Else>
</If>
```

#### `Try` / `Catch`

```xml
<Try>
  <SqlExecute Source="MyDB1" Query="UPDATE T SET Flag = 1" Var="rows" />
  <Catch Var="error">
    <Log Message="Erreur={{error}}" />
    <Return Status="500">
      <Data Var="error" />
    </Return>
  </Catch>
</Try>
```

#### `ForEach`

```xml
<ForEach>
  <In>
    <Get Name="Rows" />
  </In>
  <ItemVar>row</ItemVar>
  <IndexVar>index</IndexVar>

  <Set Name="count">
    <Value>
      <Addition>
        <Arg><Get Name="count" /></Arg>
        <Arg>1</Arg>
      </Addition>
    </Value>
  </Set>
</ForEach>
```

### 2) Appels HTTP externes

#### `Http-Get`

```xml
<Http-Get>
  <Url>https://postman-echo.com/get</Url>
  <Query>
    <Param Name="q">{{query}}</Param>
  </Query>
  <Response Var="getResponse" />
</Http-Get>
```

#### `Http-Post`

```xml
<Http-Post>
  <Url>https://postman-echo.com/post</Url>
  <Headers>
    <Header Name="Content-Type">application/json</Header>
  </Headers>
  <Body>
    <Concat>
      <Arg>{"name":"</Arg>
      <Arg><Get Name="userName" /></Arg>
      <Arg>"}</Arg>
    </Concat>
  </Body>
  <Response Var="postResponse" />
</Http-Post>
```

La variable de réponse contient:
- `statusCode`
- `isSuccess`
- `headers`
- `body` (JSON converti si possible)
- `text` (brut)

### 3) Fonctions utilitaires d'expression

Tu peux les utiliser partout où une sous-balise `<Value>` est supportée (ex: `Set`, `If/Condition`, etc.).

- Math: `Addition`, `Substract` (et alias `Subtract`), `Multiply`, `Divide`
- Texte: `Concat`, `StringFormat`, `StringSubstring`
- Conversion: `StringToInt`, `IntToString`, `StringToDateTime`
- Comparaison: `Equals`, `NotEquals`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual`
- Logique: `And`, `Or`, `Not`, `Coalesce`
- Accès structuré: `GetPath` / `GetField`

Exemple `GetPath`:

```xml
<Set Name="statusCode">
  <Value>
    <GetPath>
      <From><Get Name="httpResponse" /></From>
      <Path>statusCode</Path>
    </GetPath>
  </Value>
</Set>
```

---

## Endpoints de test ajoutés

Dans `Environement/endpoints.json`, deux endpoints de test supplémentaires sont prêts:

- `GET /test-utilities` -> `Handler/AdvancedUtilities.xml`
- `GET /test-http-trycatch` -> `Handler/HttpTryCatchDemo.xml`

### Commandes de test

```bash
curl -i http://localhost:5000/test-utilities
curl -i http://localhost:5000/test-http-trycatch
```

Si ton app écoute sur un autre port, remplace `5000`.

---

## Exemple complet

**`endpoints.json`**
```json
[
  { "path": "/users/{id}", "method": "GET", "handler": "GetUser" }
]
```

**`Handler/GetUser.xml`**
```xml
<Handler
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="../Environement.xsd">

  <Set Var="userId" Value="1" />
  <Log Message="Recherche utilisateur {{userId}}" />

  <SqlQuery
    Source="MyDB1"
    Query="SELECT Id, Name, Email FROM Users WHERE Id = {{userId}}"
    Var="result" />

  <Return Status="200" Data="{{result}}" />

</Handler>
```

---

## Démarrage

```bash
cd ExchangeAPI
dotnet run
```

Le serveur lit `endpoints.json` au démarrage et enregistre toutes les routes automatiquement. Aucune recompilation n'est nécessaire pour modifier un handler ou ajouter un endpoint.

---

## Dépendances

| Package | Usage |
|---------|-------|
| `Microsoft.AspNetCore` (.NET 10) | Serveur HTTP |
| `Microsoft.Data.SqlClient` | Connexions SQL Server |
| `Microsoft.AspNetCore.OpenApi` | Support OpenAPI |
