## Co się zmieniło i dlaczego

<!-- Kilka zdań. Jeśli karta na Trello opisuje kontekst, podlinkuj ją zamiast go przepisywać. -->

Karta na Trello:

## Checklista

- [ ] Uruchomiłem stack i użyłem tej zmiany, a nie tylko przeczytałem diff
- [ ] `docker compose exec backend dotnet test` i `docker compose exec frontend npm test` przechodzą
- [ ] Nowe zachowanie ma test (poprawka błędu ma test, który bez niej nie przechodzi)
- [ ] `python scripts/check_map.py` i `python scripts/check_text.py` kończą się zerem
- [ ] Pliki w `docs/`, których dotyczy zmiana, są zaktualizowane
- [ ] Nowe zmienne środowiskowe są w `.env.example`
- [ ] Wpis w `docs/log.md` (przy większym zadaniu)
- [ ] Checklista kryteriów akceptacji na karcie Trello odhaczona

## Bezpieczeństwo

<!-- Skreśl, jeśli zmiana nie dotyka danych ani uprawnień. -->

- [ ] Zmiana dotyka danych osobowych albo uprawnień, więc ma test negatywny sprawdzający, że dostęp jest odmawiany tam, gdzie ma być odmawiany
- [ ] Nic wrażliwego nie trafia do logów ani do komunikatów błędów pokazywanych użytkownikowi
