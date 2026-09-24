# Trello: identyfikatory i operacje

Wszystko, czego potrzebujesz, żeby ruszyć kartę bez szukania po tablicy. Agent ma dostęp do Trello przez serwer MCP; człowiek robi to samo klikając.

**Tablica:** OCWIP | Generator konkursów, <https://trello.com/b/nP2dbEcK/ocwip-generator-konkursow>
**Identyfikator tablicy:** `ari:cloud:trello::board/workspace/6a799a5b0e168fe7488e5d90/6a799b6109aa9ed71def48fd`

Karty adresuje się identyfikatorem w postaci `ari:cloud:trello::card/workspace/6a799a5b0e168fe7488e5d90/<id karty>`. Krótki kod z kolumny Trello w [`kolejka.md`](kolejka.md) daje adres do przeglądarki (`https://trello.com/c/<kod>`), ale **nie** jest identyfikatorem dla MCP: identyfikator odczytujesz przez `trelloReadCard` z akcją `get` i adresem karty.

---

## Listy na tablicy

| Lista | Do czego służy | Identyfikator |
|---|---|---|
| Zakres projektu | kontekst, roadmapa, konwencja pracy | `ari:cloud:trello::list/workspace/6a799a5b0e168fe7488e5d90/6a799ba2720f673b92c04289` |
| Backlog | karty M2 do M7 i poza MVP | `.../6a799ba61464d2c008428a49` |
| Sprint 0: research i fundament | reszta M1 | `.../6a799ba919fa3ab989438873` |
| **W trakcie** | maksymalnie trzy karty naraz | `.../6a799bad81b584098a17d3cb` |
| Błąd | karta, która wróciła z awarią | `.../6a8443c889aa87fc8f7d0451` |
| Zablokowane: czeka na klienta | karty B-xx i karty czekające na dokument | `.../6a799bb1000a9512907291d4` |
| **Do weryfikacji** | pull request otwarty, czeka na CI i merge | `.../6a799bb5b288882f77443cd6` |
| **Zrobione** | karta zamknięta, checklista odhaczona | `.../6a799bb982b5cddeae066920` |
| Decyzje projektowe | karty D1 do D15 | `.../6a799bbd375b97ee98e456f0` |

Prefiks `ari:cloud:trello::list/workspace/6a799a5b0e168fe7488e5d90/` jest wspólny, w tabeli skrócony do `...`.

## Przepływ karty przez tablicę

```
Backlog / Sprint 0  ->  W trakcie  ->  Do weryfikacji  ->  Zrobione
                            |
                            +-> Zablokowane: czeka na klienta   (brakuje dokumentu)
                            +-> Błąd                            (CI czerwone, wraca do poprawy)
```

Trzy reguły, które łatwo złamać:

1. **Maksymalnie trzy karty na liście W trakcie.** `python scripts/runbook.py doctor` to sprawdza po stronie kolejki, ale tablica jest źródłem prawdy.
2. **Karta wychodzi z listy W trakcie dopiero z całą odhaczoną checklistą.** Pozycji, której nie zrobiłeś, nie odhaczasz: dopisujesz komentarz i karta zostaje.
3. **Nowe ustalenie z zamawiającym to karta w liście Decyzje projektowe**, nie komentarz pod kartą zadania.

---

## Operacje przez MCP

Nazwy narzędzi i akcji. Szczegóły parametrów są w opisach narzędzi, tu jest to, co robisz najczęściej.

| Chcę | Narzędzie | Akcja |
|---|---|---|
| Przeczytać kartę razem z komentarzami | `trelloReadCard` | `get`, `cardIdOrUrl` = adres karty |
| Zobaczyć wszystkie karty tablicy pogrupowane listami | `trelloReadCard` | `list_by_board` |
| Zobaczyć karty jednej listy | `trelloReadCard` | `list_by_list` |
| Przeczytać checklistę kryteriów akceptacji | `trelloReadChecklist` | `list_by_card` |
| Odhaczyć pozycję checklisty | `trelloWriteChecklist` | `update_item`, `checked: true` |
| Dopisać pozycję do checklisty | `trelloWriteChecklist` | `add_item` |
| Założyć checklistę na karcie, która jej nie ma | `trelloWriteChecklist` | `create`, `name: "Kryteria akceptacji"` |
| Przenieść kartę na inną listę | `trelloWriteCard` | `move` albo `update` z `listId` |
| Dopisać komentarz | `trelloWriteCard` | `comment` |
| Założyć nową kartę | `trelloWriteCard` | `create` |

**Zanim odhaczysz cokolwiek, przeczytaj kartę.** `list_by_board` zwraca opisy, ale nie checklisty, więc identyfikatory pozycji bierzesz z `trelloReadChecklist`.

## Karty, które nie mają checklisty

Stan na 2026-09-23: checklistę "Kryteria akceptacji" mają wszystkie karty zrobione (do `T-29` włącznie, plus `T-20a`) oraz `T-12.7` i `T-12.8`. **Pozostałe karty Backlogu (od `T-30` w górę) checklisty nie mają.**

To nie jest powód do pracy bez kryteriów. Specyfikacje w `docs/runbook/M*.md` mają dla każdej takiej karty gotową listę, wyprowadzoną z opisu karty i z raportu. Zanim zaczniesz pracę nad kartą bez checklisty:

1. Weź listę z `docs/runbook/M<n>-*.md`.
2. Wpisz ją na kartę przez `trelloWriteChecklist` z akcją `create`, a potem `add_item` dla każdej pozycji.
3. Dopiero potem przenieś kartę na listę W trakcie.

Karta bez kryteriów akceptacji nie ma jak zostać zamknięta, bo definicja ukończenia wymaga odhaczonej checklisty.

---

## Identyfikatory kart zadaniowych

Kolumna "id" to część adresu MCP po `ari:cloud:trello::card/workspace/6a799a5b0e168fe7488e5d90/`.

| Karta | Kod | id |
|---|---|---|
| T-12.3 | 7MkNtmIH | `6a82daef91dbf2d6721053b1` |
| T-12.4 | mRVXGg2U | `6a82daf2d9bb0bfe23259e10` |
| T-12.5 | MZoxJ6zk | `6a82daf6ddb7d2b88d8bd620` |
| T-12.6 | YjDuR42n | `6a82daf9c22624dd9a93eed7` |
| T-12.7 | h5Dz9yj2 | `6ab3cfad29f9239b715e95a8` |
| T-12.8 | xkyhzw7r | `6ab3cfaec8674f9c9dc752b1` |
| T-13.2 | RDyoUhkh | `6a82daff7e5eb913c40722a3` |
| T-13.3 | SJflHiIR | `6a82db03f2679cf3e1bde674` |
| T-15.2 | cIONKupZ | `6a82db0cb60cdd891cd1fdd1` |
| T-15.3 | XBITHAH5 | `6a82db108e5bd866274ef95f` |
| T-15.4 | 3S2t9IdI | `6a82db1321a700d3f1208bc9` |
| T-20 | dcj9E3qW | `6a85661c97d06f43d3031b85` |
| T-20a | VMtb4Zq1 | `6ab3c77a10987391a5691536` |
| T-21 | jU68qLLX | `6a85662692f9fc29db313615` |
| T-22 | 3S8truZC | `6a8566306dab5ec19669fa48` |
| T-23 | 7PRbbgV5 | `6a85663b99d7f1a146793d9b` |
| T-24 | gcslfR97 | `6a856647f077e15e95a41d55` |
| T-25 | bl59xg1v | `6a85664ff5dc4e72c6207c25` |
| T-26 | Xw5EirNk | `6a85665af788103e75f5ce8d` |
| T-26a | 2Esmrx86 | `6ab3c77be7d70ae5bb755c88` |
| T-27 | fYqlfSoR | `6a8566615fe3aa0c08a2f37b` |
| T-28 | EJZABgdX | `6a856673a937aabef5e19f76` |
| T-29 | zw48liiX | `6a85667de479a68ff1ca691c` |
| T-30 | m5SPETCx | `6a856686260bea736217c8ff` |
| T-31 | dWHtvzvX | `6a85668fef4dee11e1436eee` |
| T-32 | K4ouKUD6 | `6a85669b9cb05967cc49aae8` |
| T-33 | uG9aepGO | `6a8566a69d8f6bf6eaf4988a` |
| T-34 | 0OWDa8wR | `6a8566ae3f2e38bcb183dfa9` |
| T-35 | GCyfm14r | `6a8566b718bd9069cf71acf9` |
| T-36 | eKXNBKtF | `6a8566c13586ab02c3579be0` |
| T-37 | 1EG1Ngzv | `6a8566d2cdf2c10f8ee1d1a9` |
| T-38 | OPGJGeOo | `6a8566dd106a83c8d6787056` |
| T-39 | j6yKe6Z6 | `6a8566e6c8160277612cd395` |
| T-40 | eHJJ5x2w | `6a8566edb733587ae408afd7` |
| T-41 | NYI7jUxv | `6a8566f35bc2ef156cd10d74` |
| T-42 | kJPn2EOF | `6a8566fca0902ff148164e11` |
| T-43 | EFTVE59t | `6a8567060a9bade72fb7f126` |
| T-44 | qRlz6aCv | `6a85670e80f02f9c16859d5b` |
| T-45 | kbHK5Nsk | `6a85671779cd47485f7f42c2` |
| T-46 | GbFFOBnp | `6a85672b7e28b74252a201eb` |
| T-47 | tG3SiRzy | `6a85673423044ee8ddf2285c` |
| T-48 | RyzKqp6D | `6a85673f8c356013ecbcd896` |
| T-49 | Tonpp3Uy | `6a8567483abdc25187981b37` |
| T-50 | 4nEU9AlG | `6a85675357d494a098e425a1` |

## Pozostałe karty

Blokery: B-01 `uOnJviAY`, B-02 `Ch6545Yd`, B-03 `WQQFgssE`, B-04 `bdcKt7iH`, B-05 `47DSAWe2`, B-06 `NSaJwkUJ`, B-07 `nOeb6e9h`, B-08 `WrXHp8iv`, B-09 `nF5CePKJ`, B-10 `lOrnpYeE`.

Decyzje: D1 `lvu3meHB`, D2 `wHRs5sds`, D3 `V9OVWXH0`, D4 `oR85sQHM`, D5 `EG4rrryK`, D6 `GKWLAlhL`, D7 `GR8foD6n`, D8 `0hQQCdoi`, D9 `ypPtjfzT`, D10 `8r4smIg5`, D11 `wNZYjElk`, D12 `9h0GBRTb`, D13 `avvr9m9F`, D14 `e5iQ5tEI`, D15 `ubMWQeSC`.

Zrobione: T-06.1 `o8NfD1QF`, T-06.2 `mWEu5NWp`, T-07 `YgjA879S`, T-07.1 `FQa7ZBxX`, T-11.1 `ZHksYfn9`, T-11.2 `WWhJrubw`, T-11.3 `u1BIcJR6`, T-11.4 `VX0vQZQg`, T-11.5 `ABKkdXCp`, T-12.0 `tg0e82xM`, T-12.1 `PpXqULSi`, T-12.2 `d4DKHGAf`, T-13.1 `iDRvclFH`, T-15.1 `mFbQQBCa`, T-17 `EVEF5QIk`.

## Etykiety

Kolory powtarzają to, co jest w tytule karty: czerwony to P0, pomarańczowy P1, żółty P2, niebieski Backend, zielony Frontend, fioletowy zablokowane. Etykiety nie mają nazw, bo Trello nie pozwala ich ustawić z poziomu integracji. **Tytuł karty jest źródłem prawdy**, nie kolor.

Format tytułu: `T-XX.Y [PRIORYTET / OBSZAR] Nazwa zadania`.
