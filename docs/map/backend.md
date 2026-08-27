# Mapa: backend

Usługa .NET (minimal API). Warstwy i wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `backend/Ocwip.slnx` | Solucja: projekt API plus projekt testów. Cel dla `dotnet restore` i `dotnet test` |
| `backend/src/Ocwip.Api/Ocwip.Api.csproj` | `net10.0`, nullable, `TreatWarningsAsErrors`. Pakiety: OpenAPI, EF Core 10, Npgsql.EF, NamingConventions, Npgsql |
| `backend/src/Ocwip.Api/Program.cs` | Wyłącznie składanie aplikacji: OpenAPI, ProblemDetails, `AppDbContext` przez `UseOcwipPostgres`, `ApplyPendingMigrations`, CORS z `Cors:Origins` (`AllowCredentials`), mapowanie endpointów. `public partial class Program` na końcu istnieje po to, żeby host testowy startował prawdziwą aplikację |
| `backend/src/Ocwip.Api/Data/AppDbContext.cs` | `DbSet` dla `Competitions` i `FormDefinitions`, konfiguracje wczytywane z assembly. `ConfigureConventions` narzuca `UtcDateTimeOffsetConverter` na każdą właściwość `DateTimeOffset`. `SaveChanges` stempluje `IAuditedEntity`, bo domyślna wartość `now()` odpala się tylko przy INSERT |
| `backend/src/Ocwip.Api/Models/IAuditedEntity.cs` | Kontrakt `CreatedAt` i `UpdatedAt`, po którym `AppDbContext` znajduje encje do stemplowania |
| `backend/src/Ocwip.Api/Data/AppDbContextFactory.cs` | `IDesignTimeDbContextFactory` dla `dotnet ef`: `Create` i `BuildConfiguration` czytają `ConnectionStrings:Postgres` z tych samych źródeł co runtime, bez niego rzucają wyjątkiem zamiast zgadywać adres |
| `backend/src/Ocwip.Api/Data/PostgresDbContextOptions.cs` | `UseOcwipPostgres`: Npgsql plus konwencja `snake_case`. Jedyne miejsce, w którym konfiguruje się model EF |
| `backend/src/Ocwip.Api/Data/Converters/UtcDateTimeOffsetConverter.cs` | Normalizuje każdy `DateTimeOffset` do UTC przed zapisem. Npgsql odrzuca timestamptz z niezerowym offsetem, więc to jest kontrakt, a nie komentarz |
| `backend/src/Ocwip.Api/Data/DatabaseStartup.cs` | `ApplyPendingMigrations`: migracje przy starcie pod flagą `Database:MigrateOnStartup`, pięć prób z narastającym opóźnieniem tylko dla błędów chwilowych |
| `backend/src/Ocwip.Api/Data/Migrations/20260819110449_InitialCreate.cs` | Migracja bazowa, pusta. `Down()` to no-op, bo `Up()` nic nie tworzy |
| `backend/src/Ocwip.Api/Data/Migrations/20260819110449_InitialCreate.Designer.cs` | Metadane EF dla `InitialCreate` (generowane) |
| `backend/src/Ocwip.Api/Data/Migrations/AppDbContextModelSnapshot.cs` | Bieżący snapshot modelu EF (generowany) |
| `backend/src/Ocwip.Api/Endpoints/HealthEndpoints.cs` | `MapHealthEndpoints`: `GET /health` (liveness) i `GET /health/db` (sonda PostgreSQL przez Npgsql, zwraca 503 i generyczny komunikat, żeby nie ujawnić hosta ani poświadczeń) |
| `backend/src/Ocwip.Api/appsettings.json` | Domyślne poziomy logowania, pusty `ConnectionStrings:Postgres`, `Database:MigrateOnStartup` fałsz, `Cors:Origins`. Wartości nadpisuje środowisko |
| `backend/src/Ocwip.Api/appsettings.Development.json` | Gadatliwsze logowanie ASP.NET Core lokalnie, `Database:MigrateOnStartup` prawda |
| `backend/tests/Ocwip.Api.Tests/Ocwip.Api.Tests.csproj` | xunit plus `Microsoft.AspNetCore.Mvc.Testing`, referencja do projektu API |
| `backend/tests/Ocwip.Api.Tests/OcwipWebApplicationFactory.cs` | `WebApplicationFactory` z wyłączonym `Database:MigrateOnStartup`. Każdy test startujący aplikację idzie tędy, żeby nie robić DDL na wspólnej bazie |
| `backend/tests/Ocwip.Api.Tests/RequiresDatabaseFactAttribute.cs` | `[RequiresDatabaseFact]` plus `ConnectionString` ze środowiska: fakt raportujący Skipped, a nie Passed, gdy nie ma bazy (xUnit 2 nie ma dynamicznego pomijania) |
| `backend/tests/Ocwip.Api.Tests/HealthEndpointsTests.cs` | Cztery testy przez `OcwipWebApplicationFactory`: `/health` zwraca 200 także przy nieosiągalnej bazie, sonda bazy zwraca 503 bez connection stringa, sonda nigdy nie zwraca w ciele hasła ani użytkownika |
| `backend/tests/Ocwip.Api.Tests/MigrationTests.cs` | Migracje na czystej bazie przez `UseOcwipPostgres` (CREATE DATABASE, Migrate, DROP). Skip, gdy brak connection stringa |
| `backend/tests/Ocwip.Api.Tests/DatabaseConfigurationTests.cs` | Fabryka design-time rzuca przy braku connection stringa, `UseOcwipPostgres` daje Npgsql plus konwencję nazw |
| `backend/src/Ocwip.Api/Models/User.cs` | Model konta użytkownika zawierający relację 1:1 z `Entity.cs` i pola danych |
| `backend/src/Ocwip.Api/Models/Role.cs` | Enum zawierający trzy role |
| `backend/src/Ocwip.Api/Models/Entity.cs` | Model podmiotu |
| `backend/src/Ocwip.Api/Models/EntityType.cs` | Enum zawierający trzy typy podmiotów |
| `backend/src/Ocwip.Api/Models/Competition.cs` | Model konkursu: kwota dotacji, status, znaczniki czasu i flaga `IsActive` (bez twardego kasowania). Settery `StartDate` i `EndDate` ucinają do pełnej minuty w UTC, celowo nie konwerterem, patrz komentarz w pliku. Relacja jeden do wielu z `FormDefinition.cs` |
| `backend/src/Ocwip.Api/Models/CompetitionStatus.cs` | Enum pięciu statusów konkursu: `Draft`, `Published`, `Closed`, `Resolved`, `Archived`. W bazie zapisywany jako tekst, nie jako ordynał |
| `backend/src/Ocwip.Api/Models/FormDefinition.cs` | Model definicji formularza: struktura jako `JsonElement` w kolumnie jsonb plus numer wersji, unikalny w obrębie konkursu |
| `backend/src/Ocwip.Api/Data/Configurations/CompetitionConfiguration.cs` | Konfiguracja EF Core konkursu: limity długości, `gen_random_uuid()`, `now()` dla znaczników czasu, pięć check constraints (kolejność dat, dodatnia kwota, pełna minuta na `start_date` i `end_date`, sparowanie `is_active` z `deactivated_at`) w jednym wywołaniu `ToTable`, indeks na `(status, end_date)`, FK bez kaskady |
| `backend/src/Ocwip.Api/Data/Configurations/FormDefinitionConfiguration.cs` | Konfiguracja EF Core definicji formularza: kolumna jsonb, trzy check constraints (sparowanie soft delete, `version_number > 0`, korzeń JSON-a jako obiekt albo tablica), unikalny indeks na `(competition_id, version_number)` |
| `backend/src/Ocwip.Api/Data/Migrations/20260827130815_AddDataModels.cs` | Tabele `competitions` i `form_definitions`, pięć check constraints na `competitions` i trzy na `form_definitions`, indeks na `(status, end_date)` dla listy publicznej, unikalny indeks na wersję formularza |
| `backend/src/Ocwip.Api/Data/Migrations/20260827130815_AddDataModels.Designer.cs` | Metadane EF dla `AddDataModels` (generowane) |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionConfigurationTests.cs` | Metadane konkursu: nazwa tabeli, klucz, limity długości, status jako tekst |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionTimestampConfigurationTests.cs` | Metadane czasu w konkursie: pełna precyzja znaczników audytowych, pełna minuta w oknie, brak konwertera ucinającego |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionSchemaConfigurationTests.cs` | Metadane schematu konkursu: wszystkie check constraints, indeks listy publicznej, FK bez kaskady |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/FormDefinitionConfigurationTests.cs` | Metadane modelu definicji formularza: nazwa tabeli, kolumna jsonb, unikalny indeks na `(competition_id, version_number)` |
| `backend/tests/Ocwip.Api.Tests/Data/TestModel.cs` | Model EF budowany tak jak w aplikacji, przez `IDesignTimeModel`, bez łączenia z bazą. Model runtime gubi check constraints i komentarze |
| `backend/tests/Ocwip.Api.Tests/Data/PostgresDatabaseFixture.cs` | Jednorazowa baza na klasę testową: CREATE DATABASE, migracje, DROP. Milczy bez connection stringa, bo xUnit tworzy fixture nawet dla pominiętych testów |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionDatabaseTests.cs` | Reszta niezmienników konkursu na prawdziwym PostgreSQL: kwota, status jako tekst, szerokości kolumn, insert omijający EF |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionWindowDatabaseTests.cs` | Okno konkursu na prawdziwym PostgreSQL: kolejność dat, pełna minuta na obu końcach, offset `+02:00`, regresja na operandzie zapytania |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionLifecycleDatabaseTests.cs` | Soft delete i znaczniki audytowe na prawdziwym PostgreSQL: sparowanie `is_active` z `deactivated_at`, ruch `updated_at` |
| `backend/tests/Ocwip.Api.Tests/Data/FormDefinitionConstraintDatabaseTests.cs` | Czego schemat nie przyjmie w definicji formularza: `version_number` niedodatni, korzeń JSON-a jako skalar |
| `backend/tests/Ocwip.Api.Tests/Data/TestCompetition.cs` | Konkurs spełniający wszystkie check constraints, żeby test mówił tylko o swoim jednym polu |
| `backend/tests/Ocwip.Api.Tests/Data/PostgresAssert.cs` | Wyłuskuje `PostgresException` z `DbUpdateException` plus stałe SQLSTATE, żeby test twierdził o nazwie constraintu, nie o komunikacie |
| `backend/tests/Ocwip.Api.Tests/Data/FormDefinitionDatabaseTests.cs` | Niezmienniki definicji formularza na prawdziwym PostgreSQL: unikalna wersja w konkursie, FK bez kaskady, round trip jsonb |


## Czego tu jeszcze nie ma

`Domain/` (encje), `Services/` (logika biznesowa), `Contracts/` (modele request i response), uwierzytelnianie i autoryzacja. Każde ma kartę na Trello. Katalogi zakładamy razem z pierwszym prawdziwym plikiem.
