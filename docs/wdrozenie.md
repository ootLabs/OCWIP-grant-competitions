# Wdrożenie

Jak postawić system na serwerze i przygotować pierwszy konkurs. Plik zakłada T-96 (treść startowa); compose produkcyjne, proxy, kopie zapasowe i procedurę wydania dokładają T-111 i dalsze zadania z [`runbook/plan-v1.md`](runbook/plan-v1.md), etap 2.

Obrazy produkcyjne opisuje [`map/infra.md`](map/infra.md) (`backend/Dockerfile.prod`, `frontend/Dockerfile.prod`, T-110).

## Pierwszy konkurs na pustej bazie (T-96)

Na świeżej instalacji konkurs nie ma formularza ani kart oceny, a bez nich nie da się go opublikować (T-97). Kolejność:

1. **Konto operatora.** Pracownik OCWIP zakłada konto na stronie rejestracji i potwierdza adres z maila.
2. **Rola operatora.** Z powłoki serwera, w kontenerze API:

   ```bash
   docker compose exec backend dotnet Ocwip.Api.dll grant-role --email adres@ocwip.pl --role Operator
   ```

   Rola nigdy nie jest nadawana przez HTTP ([`architektura.md`](architektura.md), "Rola operatora nadawana komendą").
3. **Konkurs.** Operator zakłada konkurs kreatorem w panelu i zapisuje szkic. Identyfikator konkursu jest w adresie strony konkursu: `/panel/operator/competitions/<id>`.
4. **Treść startowa.** Formularz wniosku i obie karty oceny z plików, które obraz API ma w `/app/seed`:

   ```bash
   docker compose exec backend dotnet Ocwip.Api.dll import-content \
     --competition <id> \
     --application seed/forms/application-2026.json \
     --formal seed/evaluation-cards/formal-2026.json \
     --merit seed/evaluation-cards/merit-2026.json
   ```

   Każdy plik przechodzi najpierw bramkę kontraktu formularza. Jeśli któryś nie przechodzi, komenda wypisuje powody i niczego nie publikuje. Plik identyczny z wersją w mocy niczego nie zmienia, więc komendę można powtórzyć. Wzór sprawozdania (`--report`) dojdzie z T-95; do tego czasu operator kopiuje go na stronie konkursu z innego konkursu albo publikuje później.
5. **Publikacja.** Operator sprawdza na stronie konkursu, że lista braków jest pusta, i publikuje.

Następny konkurs zaczyna się zwykle od poprzedniego: na stronie konkursu sekcja "Karty oceny i wzór sprawozdania" kopiuje wersje w mocy z wybranego konkursu, a formularz wniosku kopiuje kreator formularza.

Lokalnie, na stosie deweloperskim, ta sama komenda idzie przez `dotnet run`:

```bash
docker compose exec backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj --no-launch-profile \
  -- import-content --competition <id> --application seed/forms/application-2026.json
```
