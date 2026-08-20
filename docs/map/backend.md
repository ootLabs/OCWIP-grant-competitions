# Mapa: backend

Usługa .NET (minimal API). Warstwy i wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `backend/Ocwip.slnx` | Solucja: projekt API plus projekt testów. Cel dla `dotnet restore` i `dotnet test` |
| `backend/src/Ocwip.Api/Ocwip.Api.csproj` | `net10.0`, nullable, `TreatWarningsAsErrors`. Pakiety: `Microsoft.AspNetCore.OpenApi`, `Npgsql` |
| `backend/src/Ocwip.Api/Program.cs` | Wyłącznie składanie aplikacji: OpenAPI, ProblemDetails, polityka CORS z `Cors:Origins` (z `AllowCredentials`, bo sesja jedzie ciasteczkiem), mapowanie endpointów. Zero logiki biznesowej. `public partial class Program` na końcu istnieje po to, żeby host testowy startował prawdziwą aplikację |
| `backend/src/Ocwip.Api/Endpoints/HealthEndpoints.cs` | `MapHealthEndpoints`: `GET /health` (liveness) i `GET /health/db` (sonda PostgreSQL przez Npgsql, zwraca 503 i generyczny komunikat, żeby nie ujawnić hosta ani poświadczeń) |
| `backend/src/Ocwip.Api/appsettings.json` | Domyślne poziomy logowania, pusty `ConnectionStrings:Postgres`, `Cors:Origins`. Wartości nadpisuje środowisko |
| `backend/src/Ocwip.Api/appsettings.Development.json` | Gadatliwsze logowanie ASP.NET Core lokalnie |
| `backend/tests/Ocwip.Api.Tests/Ocwip.Api.Tests.csproj` | xunit plus `Microsoft.AspNetCore.Mvc.Testing`, referencja do projektu API |
| `backend/tests/Ocwip.Api.Tests/HealthEndpointsTests.cs` | Trzy testy przez `WebApplicationFactory`: `/health` zwraca 200, sonda bazy zwraca 503 bez connection stringa, sonda nigdy nie zwraca w ciele hasła ani użytkownika |
| `backend/src/Ocwip.Api/Models/User.cs` | Model konta użytkownika zawierający relację 1:1 z `Entity.cs` i pola danych |
| `backend/src/Ocwip.Api/Models/Role.cs` | Enum zawierający trzy role |
| `backend/src/Ocwip.Api/Models/Entity.cs`| Model podmiotu |
| `backend/src/Ocwip.Api/Models/EntityType.cs` | Enum zawierający trzy typy podmiotów |


## Czego tu jeszcze nie ma

`Data/` (DbContext, migracje), `Domain/` (encje), `Services/` (logika biznesowa), `Contracts/` (modele request i response), uwierzytelnianie i autoryzacja. Każde ma kartę na Trello. Katalogi zakładamy razem z pierwszym prawdziwym plikiem.
