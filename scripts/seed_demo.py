#!/usr/bin/env python3
"""Fill a demo installation with a whole year of a grant programme, the way
people would have made it: through the API, never beside it.

seed.py writes the smallest fixture straight into an empty local database.
This one is for a demo that a team tries on its own: every account registers,
confirms its address from the mail in Mailpit and gets its role from the
server command, and every competition, application, card, contract and report
goes through the same endpoints the screens use. What the product cannot do,
this cannot do either, so whatever it leaves behind is a state the product
can be in.

What it leaves (docs/wdrozenie.md, "Dane demo"):
  1/2026  open intake: a submitted application, a draft, one returned for
          corrections, a group with a patron
  2/2026  under review: formal cards (one negative), a committee of two
          experts with declarations, merit cards finished and in progress
  3/2026  resolved: approved results and their mails, shared cards, signed
          contracts, a resignation and a grant from the reserve list, reports
          submitted, returned and accepted
  4/2025  archived, its report returned for corrections
  5/2026  continuous intake, open
  6/2026  announced: published, intake not started yet
  7/2026  draft, seen by operators only
plus a card shared by two people and an access request waiting for an answer.

Usage (a local stack with Mailpit; the dev compose file carries Cloudflare's
test Turnstile secret, which accepts the test token below):

    python scripts/seed_demo.py --suffix t1 --credentials .demo-accounts.local

On a server the API it talks to must carry the test secret too; the public
one keeps the real key (docs/wdrozenie.md). Standard library only.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import random
import re
import secrets
import shlex
import string
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass, field
from pathlib import Path

# Cloudflare's published test token: only a backend with the test secret takes it.
TEST_TOKEN = "XXXX.DUMMY.TOKEN.XXXX"
TOKEN_HEADER = "X-Turnstile-Token"

# The smallest file the upload recognises as a PDF by its bytes.
PDF = b"%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n"

CONTENT = [
    "--application", "seed/forms/application-2026.json",
    "--formal", "seed/evaluation-cards/formal-2026.json",
    "--merit", "seed/evaluation-cards/merit-2026.json",
    "--report", "seed/forms/report-2026.json",
    "--contract", "seed/templates/contract-2026.txt",
]


class SeedError(Exception):
    pass


@dataclass
class Person:
    key: str
    first: str
    last: str
    role: str
    note: str
    email: str = ""
    cookies: dict[str, str] = field(default_factory=dict)
    id: str | None = None


@dataclass
class Settings:
    api: str
    host: str | None
    mailpit: str
    admin: list[str]
    token: str
    suffix: str
    password: str
    domain: str


S: Settings


def say(text: str) -> None:
    print(text, flush=True)


# --- HTTP -------------------------------------------------------------------

def call(method: str, path: str, body=None, *, who: Person | None = None,
         multipart: tuple[bytes, str] | None = None, ok=(200, 201, 202, 204)):
    """One request; a refusal stops the run with what the server said."""
    headers = {"Accept": "application/json", TOKEN_HEADER: S.token}
    data = None
    if multipart is not None:
        data, boundary = multipart
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if S.host:
        headers["Host"] = S.host
    if who is not None and who.cookies:
        headers["Cookie"] = "; ".join(f"{k}={v}" for k, v in who.cookies.items())

    for attempt in range(4):
        request = urllib.request.Request(S.api + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                status, raw, cookies = response.status, response.read(), response.headers.get_all("Set-Cookie") or []
        except urllib.error.HTTPError as error:
            status, raw, cookies = error.code, error.read(), error.headers.get_all("Set-Cookie") or []
            # The per-address limit on the account routes: wait it out, as a person would.
            if status == 429 and attempt < 3:
                wait = min(int(error.headers.get("Retry-After") or 60), 120)
                say(f"    (limit żądań, czekam {wait} s)")
                time.sleep(wait)
                continue
        break

    if who is not None:
        # The session cookie is Secure in Production; kept by hand, so a run
        # over the internal network's plain HTTP keeps it all the same.
        for cookie in cookies:
            name, _, rest = cookie.partition("=")
            who.cookies[name.strip()] = rest.split(";", 1)[0]

    if status not in ok:
        raise SeedError(f"{method} {path} answered {status}: {raw.decode(errors='replace')[:2000]}")
    if not raw:
        return None
    try:
        return json.loads(raw)
    except ValueError:
        return raw


def form(fields: dict[str, str], file_name: str, content: bytes) -> tuple[bytes, str]:
    boundary = uuid.uuid4().hex
    parts = []
    for name, value in fields.items():
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode())
    parts.append(
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{file_name}\"\r\n"
        f"Content-Type: application/pdf\r\n\r\n".encode() + content + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), boundary


def admin(*args: str) -> str:
    result = subprocess.run([*S.admin, *args], capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise SeedError(f"{args[0]} failed: {result.stdout[-400:]} {result.stderr[-400:]}")
    return result.stdout


def mail(to: str, subject: str, pattern: str) -> re.Match:
    """The newest mail to this address with this subject, waited for."""
    query = urllib.parse.quote(f'to:"{to}" subject:"{subject}"')
    for _ in range(60):
        with urllib.request.urlopen(f"{S.mailpit}/search?query={query}&limit=1", timeout=30) as response:
            found = json.loads(response.read()).get("messages") or []
        if found:
            with urllib.request.urlopen(f"{S.mailpit}/message/{found[0]['ID']}", timeout=30) as response:
                text = json.loads(response.read()).get("Text", "")
            match = re.search(pattern, text)
            if match:
                return match
        time.sleep(1)
    raise SeedError(f"no mail '{subject}' to {to} in Mailpit")


# --- people -----------------------------------------------------------------

def email_of(person: Person) -> str:
    local = person.key if not S.suffix else f"{person.key}.{S.suffix}"
    return f"{local}@{S.domain}"


CONSENTS: list[str] = []


def register(person: Person) -> Person:
    person.email = email_of(person)
    if not CONSENTS:
        CONSENTS.extend(item["version"] for item in call("GET", "/public/consents"))
    call("POST", "/register", {
        "email": person.email, "password": S.password,
        "firstName": person.first, "lastName": person.last,
        "returnUrl": None, "acceptedConsents": CONSENTS,
    })
    link = mail(person.email, "Potwierdzenie adresu", r"userId=([0-9a-fA-F-]{36})&token=([^\s&]+)")
    call("POST", "/verify-email", {"userId": link.group(1), "token": urllib.parse.unquote(link.group(2))})
    if person.role != "Applicant":
        admin("grant-role", "--email", person.email, "--role", person.role)
    login(person)
    person.id = call("GET", "/me", who=person).get("id")
    say(f"  konto {person.email} ({person.note})")
    return person


def login(person: Person) -> None:
    person.cookies.clear()
    call("POST", "/login", {"email": person.email, "password": S.password, "returnUrl": None}, who=person)


def nip() -> str:
    weights = [6, 5, 7, 2, 3, 4, 5, 6, 7]
    while True:
        digits = [random.randint(1, 9)] + [random.randint(0, 9) for _ in range(8)]
        check = sum(d * w for d, w in zip(digits, weights)) % 11
        if check != 10:
            return "".join(map(str, digits + [check]))


def nrb() -> str:
    """26 digits with a valid IBAN checksum for PL."""
    while True:
        bban = "".join(random.choice(string.digits) for _ in range(24))
        check = 98 - int(bban + "252100") % 97
        return f"{check:02d}{bban}"


def card(person: Person, kind: str, name: str, **extra) -> dict:
    body = {"type": kind, "name": name}
    if kind in ("Organisation", "PatronInformalGroup"):
        body.update({
            "legalForm": extra.get("legalForm", "Association"),
            "register": "Krs", "registerNumber": f"{random.randint(1, 999999):010d}",
            "nip": extra.get("nip") or nip(),
            "address": extra.get("address", "ul. Ozimska 10, 45-057 Opole"),
            "phone": "+48 77 400 10 20", "email": f"biuro.{person.key}@{S.domain}",
            "bankAccount": nrb(),
            "representatives": extra.get("representatives",
                                         [{"firstName": person.first, "lastName": person.last, "function": "Prezes zarządu"}]),
        })
    created = call("POST", "/me/entities", body, who=person)
    return {"id": created["id"], "nip": body.get("nip")}


# --- applications -----------------------------------------------------------

def cost(name: str, count: int, price: int) -> dict:
    return {"nazwa": name, "jednostka": "szt.", "liczba": count, "cena": price}


def member(name: str) -> dict:
    return {"imie_i_nazwisko": name, "adres": "ul. Kościuszki 3, Brzeg", "telefon": "700 100 200", "email": None}


def answers(kind: str, title: str, *, town: str, budget: int) -> dict:
    """A complete 2026 application (e2e/fixtures/answers-2026.ts, varied)."""
    body = {
        "rodzaj_wnioskodawcy": kind,
        "gmina_realizacji": town,
        "charakterystyka_wnioskodawcy": "Działamy od kilku lat na rzecz mieszkańców, głównie w oparciu o wolontariuszy. Dane fikcyjne do demonstracji.",
        "tytul_projektu": title,
        "data_zakonczenia": f"{dt.date.today().year}-11-30",
        "charakterystyka_projektu": f"{title}: cykl spotkań, warsztatów i jedno wydarzenie otwarte dla mieszkańców.",
        "cel_glowny": "Więcej wspólnych działań sąsiedzkich i lepsza integracja mieszkańców.",
        "opis_pomyslu": ("Mieszkańcy od dawna zgłaszają, że brakuje miejsca i okazji do wspólnego działania. "
                         "Projekt odpowiada na tę potrzebę, angażując lokalnych liderów i wolontariuszy. ") * 8,
        "opis_dzialan": "1. Spotkanie otwierające. 2. Sześć warsztatów. 3. Wydarzenie podsumowujące. 4. Ewaluacja z uczestnikami.",
        "promocja": "Plakaty w szkole i bibliotece, profil w mediach społecznościowych, informacja na stronie gminy.",
        "rezultaty_opis": "Powstaje stała grupa mieszkańców, która kontynuuje działania po zakończeniu projektu.",
        "liczba_uczestnikow": random.choice([25, 40, 60, 80]),
        "liczba_uczestnikow_monitorowanie": "Listy obecności",
        "rezultaty": [{"rezultat": "Promocja projektu", "wartosc_docelowa": "20 plakatów", "monitorowanie": "Zdjęcia"}],
        "dostepnosc_architektoniczna": "Zajęcia w budynku bez barier, z windą i toaletą dla osób z niepełnosprawnościami.",
        "dostepnosc_cyfrowa": "Ogłoszenia z tekstem alternatywnym i napisami w nagraniach.",
        "dostepnosc_informacyjna": "Informacja o projekcie w tekście łatwym do czytania.",
        "koszty_bezposrednie": [cost("Materiały warsztatowe", 1, budget - 700), cost("Wynajem sali", 2, 100)],
        "koszty_promocji": [cost("Plakaty", 10, 20)],
        "koszty_posrednie": [cost("Obsługa księgowa", 1, 300)],
        "o_zwiazanie": True, "o_zgodnosc": True, "o_niekaralnosc": True, "o_pozytek": True,
        "o_regulamin": True, "o_rodo": True, "o_rodo_osoby_trzecie": True,
    }
    if kind == "Organisation":
        body.update({"rodzaj_organizacji": "lokalna", "data_wpisu": "2021-03-01", "przychod": 24000,
                     "o_siedziba": True, "o_podatki": True, "o_skladki": True})
    else:
        body.update({"nazwa_grupy": title.split(":")[0],
                     "czlonkowie_grupy": [member("Ewa Sowa"), member("Adam Kruk"), member("Iga Wilk")],
                     "o_mieszkancy": True})
        if kind == "InformalGroup":
            body["rachunek_lidera"] = nrb()
        else:
            body.update({"o_siedziba": True, "o_podatki": True, "o_skladki": True,
                         "o_brak_powiazan": True, "o_dane_patrona": True})
    return body


@dataclass
class Competition:
    id: str
    number: str
    requirement: str | None = None


def competition(operator: Person, number: str, title: str, *, start: dt.datetime, end: dt.datetime | None,
                continuous: bool = False, pool: int = 21000, content: bool = True, publish: bool = True) -> Competition:
    number = number if not S.suffix else f"{number}-{S.suffix}"
    created = call("POST", "/competitions", {
        "number": number, "title": title,
        "description": "Konkurs demonstracyjny z danymi fikcyjnymi. Regulamin, karty oceny i wzór umowy jak w 2026.",
        "startDate": iso(start), "endDate": None if continuous else iso(end),
        "isContinuousIntake": continuous, "maxGrantAmount": 7000, "totalPoolAmount": pool,
        "formDefinitionId": None, "requiresPaperSubmission": False,
        "maxIndirectCostPercent": 10, "maxAverageAnnualRevenue": 50000,
        "attachments": [{"title": "Statut organizacji albo regulamin grupy", "description": None,
                         "requirement": "Required", "allowedFormats": ["Pdf"]}],
    }, who=operator)
    item = Competition(created["id"], number)
    if content:
        admin("import-content", "--competition", item.id, *CONTENT)
        call("PUT", f"/competitions/{item.id}/evaluation-settings", {
            "evaluatorsPerApplication": 2, "scoreAggregation": "Sum", "meritThreshold": 50,
            "thresholdIncludesStrategic": False, "divergenceThresholdPercent": 30,
        }, who=operator)
    if publish:
        status(operator, item, "Published")
        public = call("GET", f"/public/competitions/{item.id}")
        item.requirement = (public.get("attachments") or [{}])[0].get("id")
    say(f"  konkurs {number}: {title}")
    return item


def status(operator: Person, item: Competition, to: str) -> None:
    call("POST", f"/competitions/{item.id}/status", {"status": to}, who=operator)


def iso(moment: dt.datetime) -> str:
    return moment.replace(second=0, microsecond=0).astimezone(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def apply(person: Person, item: Competition, kind: str, title: str, *, town: str, budget: int = 6000,
          submit: bool = True) -> str:
    draft = call("POST", f"/competitions/{item.id}/applications", None, who=person)
    call("PUT", f"/applications/{draft['id']}", {"answers": answers(kind, title, town=town, budget=budget)}, who=person)
    if item.requirement:
        call("POST", f"/applications/{draft['id']}/attachments",
             multipart=form({"requirementId": item.requirement}, "statut.pdf", PDF), who=person)
    if submit:
        call("POST", f"/applications/{draft['id']}/submit", None, who=person)
    return draft["id"]


def formal(operator: Person, application: str, kind: str, positive: bool = True) -> None:
    card_id = call("POST", f"/applications/{application}/evaluations/formal", None, who=operator)["id"]
    body = {"zlozony_w_terminie": True, "uprawniony_wnioskodawca": positive, "siedziba_w_wojewodztwie": True,
            "dzialania_w_wojewodztwie": True, "dzialania_w_terminie": True, "kwota_do_7000": True}
    if kind == "Organisation":
        body.update({"przychod_do_50000": True, "rejestracja_do_60_miesiecy": True})
    call("PUT", f"/evaluations/{card_id}", {"answers": body}, who=operator)
    call("POST", f"/evaluations/{card_id}/finish", None, who=operator)


def merit(expert: Person, application: str, kind: str, points: tuple[int, int, int, int], *, finish: bool = True) -> None:
    card_id = call("POST", f"/applications/{application}/evaluations/merit", None, who=expert)["id"]
    idea, results, promo, budget = points
    body = {"pomysl_i_cel": idea, "rezultaty": results, "promocja": promo, "budzet": budget,
            "proponowana_kwota": 5500,
            "pomysl_i_cel_uzasadnienie": "Pomysł dobrze osadzony w potrzebach mieszkańców.",
            "rezultaty_uzasadnienie": "Rezultaty mierzalne, trwałość opisana.",
            "promocja_uzasadnienie": "Promocja wystarczająca do skali projektu.",
            "budzet_uzasadnienie": "Koszty adekwatne do działań.", "biale_plamy": False}
    body["bez_wsparcia_nowefio" if kind == "Organisation" else "grupa_z_patronem"] = kind == "PatronInformalGroup"
    call("PUT", f"/evaluations/{card_id}", {"answers": body}, who=expert)
    if finish:
        call("POST", f"/evaluations/{card_id}/finish", None, who=expert)


def committee(operator: Person, item: Competition, experts: list[Person], applications: list[str]) -> None:
    for expert in experts:
        call("POST", f"/competitions/{item.id}/experts", {"email": expert.email}, who=operator)
        for application in applications:
            call("POST", f"/applications/{application}/assignments", {"reviewerId": expert.id}, who=operator)
        call("POST", f"/reviewer/competitions/{item.id}/declaration", {"accept": True, "refusalReason": None}, who=expert)


def close_intake(operator: Person, item: Competition, days_ago: int) -> None:
    """Ends the intake in the past, after the submissions, which closes it."""
    current = call("GET", f"/competitions/{item.id}", who=operator)
    now = dt.datetime.now(dt.timezone.utc)
    body = {k: current.get(k) for k in (
        "number", "title", "description", "isContinuousIntake", "maxGrantAmount", "totalPoolAmount",
        "formDefinitionId", "requiresPaperSubmission", "maxIndirectCostPercent", "maxAverageAnnualRevenue")}
    body.update({"startDate": iso(now - dt.timedelta(days=days_ago + 30)),
                 "endDate": iso(now - dt.timedelta(days=days_ago)),
                 "attachments": current.get("attachments") or []})
    # The state follows the dates: an intake that ended is closed already.
    call("PUT", f"/competitions/{item.id}", body, who=operator)


# --- the year ---------------------------------------------------------------

def run() -> list[Person]:
    now = dt.datetime.now(dt.timezone.utc)
    say("Konta")
    anna = register(Person("anna.kowalska", "Anna", "Kowalska", "Operator", "operator OCWIP"))
    piotr = register(Person("piotr.zielinski", "Piotr", "Zieliński", "Operator", "drugi operator"))
    jan = register(Person("jan.nowicki", "Jan", "Nowicki", "Reviewer", "ekspert"))
    ewa = register(Person("ewa.mazur", "Ewa", "Mazur", "Reviewer", "ekspertka"))
    marek = register(Person("marek.nowak", "Marek", "Nowak", "Applicant", "Stowarzyszenie Aktywne Opole, założyciel karty"))
    joanna = register(Person("joanna.lis", "Joanna", "Lis", "Applicant", "Stowarzyszenie Aktywne Opole, dołączyła przez prośbę o dostęp"))
    kasia = register(Person("katarzyna.wisniewska", "Katarzyna", "Wiśniewska", "Applicant", "grupa nieformalna Sąsiedzi z Zaodrza"))
    aga = register(Person("agnieszka.kaminska", "Agnieszka", "Kamińska", "Applicant", "Fundacja Zielony Śląsk"))
    pawel = register(Person("pawel.lewandowski", "Paweł", "Lewandowski", "Applicant", "Klub Sportowy Orzeł Nysa"))
    magda = register(Person("magdalena.dabrowska", "Magdalena", "Dąbrowska", "Applicant", "patron grupy Młodzi z Brzegu"))
    krzysztof = register(Person("krzysztof.wrobel", "Krzysztof", "Wróbel", "Applicant", "czeka na dostęp do karty Fundacji Zielony Śląsk"))
    people = [anna, piotr, jan, ewa, marek, joanna, kasia, aga, pawel, magda, krzysztof]

    say("Karty organizacji")
    aktywne = card(marek, "Organisation", "Stowarzyszenie Aktywne Opole")
    card(kasia, "InformalGroup", "Sąsiedzi z Zaodrza")
    zielony = card(aga, "Organisation", "Fundacja Zielony Śląsk", legalForm="Foundation")
    card(pawel, "Organisation", "Klub Sportowy Orzeł Nysa")
    card(magda, "PatronInformalGroup", "Stowarzyszenie Brzeg Razem (patron grupy Młodzi z Brzegu)")
    # RD7: a second person joins a card by its NIP, the founder says yes; another waits.
    call("POST", "/me/access-requests", {"nip": aktywne["nip"]}, who=joanna)
    pending = call("GET", f"/me/entities/{aktywne['id']}/access-requests", who=marek)
    for request in pending:
        call("POST", f"/me/entities/{aktywne['id']}/access-requests/{request['id']}/decision", {"approve": True}, who=marek)
    call("POST", "/me/access-requests", {"nip": zielony["nip"]}, who=krzysztof)

    say("3/2026 rozstrzygnięty: wyniki, umowy, rezygnacja, lista rezerwowa, sprawozdania")
    c3 = competition(anna, "3/2026", "Kierunek NOWE FIO 2026: inicjatywy sąsiedzkie",
                     start=now - dt.timedelta(hours=2), end=now + dt.timedelta(days=1), pool=12000)
    a_marek = apply(marek, c3, "Organisation", "Sąsiedzka biblioteka pod chmurką", town="Opole")
    a_aga = apply(aga, c3, "Organisation", "Zielone podwórka Śląska", town="Prudnik")
    a_kasia = apply(kasia, c3, "InformalGroup", "Sąsiedzi z Zaodrza: warsztaty naprawcze", town="Opole")
    a_magda = apply(magda, c3, "PatronInformalGroup", "Młodzi z Brzegu: kino plenerowe", town="Brzeg")
    a_pawel = apply(pawel, c3, "Organisation", "Orlik dla wszystkich", town="Nysa")
    close_intake(anna, c3, days_ago=40)
    status(anna, c3, "UnderReview")
    for application, kind in ((a_marek, "Organisation"), (a_aga, "Organisation"), (a_kasia, "InformalGroup"),
                              (a_magda, "PatronInformalGroup"), (a_pawel, "Organisation")):
        formal(anna, application, kind)
    scored = {a_marek: ("Organisation", (18, 14, 9, 4)), a_aga: ("Organisation", (17, 13, 8, 4)),
              a_magda: ("PatronInformalGroup", (16, 12, 8, 3)), a_kasia: ("InformalGroup", (14, 10, 7, 3)),
              a_pawel: ("Organisation", (8, 6, 4, 2))}
    committee(anna, c3, [jan, ewa], list(scored))
    for expert in (jan, ewa):
        for application, (kind, points) in scored.items():
            merit(expert, application, kind, points)
    for application, grant in ((a_marek, 5500), (a_aga, 6000)):
        call("PUT", f"/applications/{application}/grant-decision", {"awardedGrant": grant, "note": None}, who=anna)
    call("PUT", f"/applications/{a_magda}/grant-decision", {"awardedGrant": 500, "note": "Kwota ograniczona pulą konkursu."}, who=anna)
    call("POST", f"/competitions/{c3.id}/results/approve", None, who=anna)
    call("POST", f"/competitions/{c3.id}/result-notifications/send", None, who=anna)
    call("POST", f"/competitions/{c3.id}/card-sharing", None, who=anna)
    # Magda's group resigns; the freed money goes to Kasia's group from the reserve list.
    call("POST", f"/applications/{a_magda}/resignation", None, who=anna)
    call("POST", f"/applications/{a_kasia}/promotion", {"awardedGrant": 500}, who=anna)
    signed = [sign(anna, a_marek, days_ago=30), sign(anna, a_aga, days_ago=30)]
    contract(anna, a_kasia)  # drawn up, waiting for the signature
    report(marek, a_marek, 5500, outcome="submitted")
    report(aga, a_aga, 6000, outcome="accepted", operator=anna)
    del signed

    say("4/2025 archiwalny")
    c4 = competition(anna, "4/2025", "Lokalne inicjatywy 2025 (archiwum)",
                     start=now - dt.timedelta(hours=2), end=now + dt.timedelta(days=1), pool=7000)
    a4 = apply(aga, c4, "Organisation", "Rowerem po Opolszczyźnie", town="Krapkowice")
    close_intake(anna, c4, days_ago=300)
    status(anna, c4, "UnderReview")
    formal(anna, a4, "Organisation")
    committee(anna, c4, [jan, ewa], [a4])
    for expert in (jan, ewa):
        merit(expert, a4, "Organisation", (17, 13, 8, 4))
    call("PUT", f"/applications/{a4}/grant-decision", {"awardedGrant": 6000, "note": None}, who=anna)
    call("POST", f"/competitions/{c4.id}/results/approve", None, who=anna)
    sign(anna, a4, days_ago=200)
    report(aga, a4, 6000, outcome="returned", operator=anna)
    status(anna, c4, "Archived")

    say("2/2026 w ocenie: karty formalne, komisja, oceny merytoryczne w toku")
    c2 = competition(anna, "2/2026", "Młodzi aktywni 2026",
                     start=now - dt.timedelta(hours=2), end=now + dt.timedelta(days=1), pool=21000)
    b_marek = apply(marek, c2, "Organisation", "Młodzieżowa rada osiedla", town="Opole")
    b_aga = apply(aga, c2, "Organisation", "Ekoszkoła w Prudniku", town="Prudnik")
    b_kasia = apply(kasia, c2, "InformalGroup", "Sąsiedzi z Zaodrza: podwórkowa liga", town="Opole")
    b_pawel = apply(pawel, c2, "Organisation", "Turniej orlików", town="Nysa")
    b_magda = apply(magda, c2, "PatronInformalGroup", "Młodzi z Brzegu: radio szkolne", town="Brzeg")
    close_intake(anna, c2, days_ago=5)
    status(anna, c2, "UnderReview")
    formal(anna, b_pawel, "Organisation", positive=False)  # formally rejected
    for application, kind in ((b_marek, "Organisation"), (b_aga, "Organisation"),
                              (b_kasia, "InformalGroup"), (b_magda, "PatronInformalGroup")):
        formal(anna, application, kind)
    committee(anna, c2, [jan, ewa], [b_marek, b_aga, b_kasia, b_magda])
    merit(jan, b_marek, "Organisation", (17, 12, 8, 4))
    merit(ewa, b_marek, "Organisation", (16, 13, 9, 4))
    merit(jan, b_kasia, "InformalGroup", (12, 9, 6, 3))
    merit(ewa, b_kasia, "InformalGroup", (19, 14, 9, 4))  # far apart from Jan's: a divergence to resolve
    merit(jan, b_magda, "PatronInformalGroup", (15, 11, 7, 3), finish=False)  # in progress

    say("1/2026 otwarty nabór: złożony, szkic, zwrócony do poprawy")
    c1 = competition(anna, "1/2026", "Inicjatywy lokalne 2026",
                     start=now - dt.timedelta(days=10), end=now + dt.timedelta(days=30), pool=35000)
    apply(marek, c1, "Organisation", "Sąsiedzka biblioteka: druga edycja", town="Opole")
    apply(kasia, c1, "InformalGroup", "Sąsiedzi z Zaodrza: ogród społeczny", town="Opole", submit=False)
    returned = apply(aga, c1, "Organisation", "Kwietne łąki w mieście", town="Prudnik")
    call("POST", f"/applications/{returned}/return", {
        "sections": ["projekt", "budzet"], "unlocksAttachments": True,
        "message": "Proszę uzupełnić opis dostępności i poprawić budżet (koszty pośrednie powyżej limitu).",
        "deadline": iso(now + dt.timedelta(days=7)),
    }, who=anna)
    apply(magda, c1, "PatronInformalGroup", "Młodzi z Brzegu: mural na skwerze", town="Brzeg")

    say("Pozostałe konkursy")
    competition(anna, "5/2026", "Mikrogranty 2026 (nabór ciągły)", start=now - dt.timedelta(days=20), end=None,
                continuous=True, pool=20000)
    competition(anna, "6/2026", "Senioralne inicjatywy 2026 (nabór od przyszłego miesiąca)",
                start=now + dt.timedelta(days=30), end=now + dt.timedelta(days=60), pool=28000)
    competition(anna, "7/2026", "Kultura w gminach 2027 (szkic, widoczny tylko dla operatorów)",
                start=now + dt.timedelta(days=90), end=now + dt.timedelta(days=120), publish=False)
    return people


def contract(operator: Person, application: str) -> dict:
    drawn = call("POST", f"/applications/{application}/contract", None, who=operator)
    values = {f["name"]: f"Wartość demo: {f['name']}" for f in drawn.get("fields", []) if not f.get("system")}
    if values:
        call("PUT", f"/contracts/{drawn['id']}/values", {"values": values}, who=operator)
    return drawn


def sign(operator: Person, application: str, days_ago: int) -> str:
    drawn = contract(operator, application)
    day = (dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=days_ago)).date().isoformat()
    call("POST", f"/contracts/{drawn['id']}/sign", {"signedOn": day}, who=operator)
    return drawn["id"]


def report(applicant: Person, application: str, grant: int, *, outcome: str, operator: Person | None = None) -> None:
    created = call("POST", f"/applications/{application}/report", None, who=applicant)
    current = call("GET", f"/reports/{created['id']}", who=applicant)
    filled = dict(current.get("answers") or {})
    # What was spent, row by row, the grant spread over the costs in proportion.
    tables = ("koszty_bezposrednie", "koszty_promocji", "koszty_posrednie")
    total = sum(row.get("liczba", 0) * row.get("cena", 0) for t in tables for row in filled.get(t) or [])
    number = 1
    for t in tables:
        for row in filled.get(t) or []:
            value = row.get("liczba", 0) * row.get("cena", 0)
            row.update({"dokument": f"FV {number}/{dt.date.today().year}", "wartosc_calkowita": value,
                        "z_dotacji": round(value * grant / total, 2) if total else 0})
            number += 1
    for row in filled.get("rezultaty") or []:
        row["osiagniety"] = row.get("wartosc_docelowa") or "Osiągnięty"
    filled.update({
        "osoba_sporzadzajaca": f"{applicant.first} {applicant.last}", "osoba_telefon": "+48 77 400 10 20",
        "osoba_email": applicant.email,
        "reprezentanci": [{"imie_i_nazwisko": f"{applicant.first} {applicant.last}", "funkcja": "Prezes zarządu"}],
        "osiagniecie_celu": "Cel osiągnięty: grupa mieszkańców działa dalej po zakończeniu projektu.",
        "opis_dzialan": ("Zrealizowano wszystkie zaplanowane działania zgodnie z harmonogramem: spotkanie otwierające, "
                         "sześć warsztatów i wydarzenie podsumowujące, w którym wzięli udział mieszkańcy i partnerzy. ") * 6,
        "zmiany": "Bez istotnych zmian.", "liczba_uczestnikow_osiagnieta": 45,
        "promocja": "Plakaty, wydarzenie na profilu, artykuł w gazecie lokalnej.",
        "grupa_dalsze_dzialanie": True, "grupa_wzrost": True,
        "organizacja_wolontariusze": True, "organizacja_wzrost": True,
    })
    call("PUT", f"/reports/{created['id']}", {"answers": filled}, who=applicant)
    call("POST", f"/reports/{created['id']}/submit", None, who=applicant)
    if outcome == "returned" and operator is not None:
        call("POST", f"/reports/{created['id']}/return",
             {"reason": "Proszę dołączyć zdjęcia z wydarzenia i poprawić numery faktur w części III."}, who=operator)
    if outcome == "accepted" and operator is not None:
        call("PUT", f"/reports/{created['id']}/cost-review", {"items": []}, who=operator)
        call("POST", f"/reports/{created['id']}/accept", None, who=operator)


def main() -> int:
    global S
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--api", default="http://localhost:8080")
    parser.add_argument("--host", default=None, help="Host header, when the API is reached by address")
    parser.add_argument("--mailpit", default="http://localhost:8025/api/v1")
    parser.add_argument("--admin", default="docker compose exec -T backend dotnet src/Ocwip.Api/bin/Debug/net10.0/Ocwip.Api.dll")
    parser.add_argument("--token", default=TEST_TOKEN)
    parser.add_argument("--suffix", default="", help="added to addresses and numbers, for runs beside other data")
    parser.add_argument("--domain", default="example.org")
    parser.add_argument("--credentials", required=True, type=Path, help="where the accounts and their password go")
    args = parser.parse_args()

    password = "Demo-" + "".join(secrets.choice(string.ascii_letters + string.digits) for _ in range(12)) + "!7"
    S = Settings(args.api.rstrip("/"), args.host, args.mailpit.rstrip("/"), shlex.split(args.admin),
                 args.token, args.suffix, password, args.domain)
    try:
        people = run()
    except SeedError as error:
        print(f"PRZERWANE: {error}", file=sys.stderr)
        return 1

    lines = ["Konta demo OCWIP (wszystkie z tym samym hasłem)", f"haslo: {password}", ""]
    lines += [f"{p.email:<44} {p.role:<10} {p.note}" for p in people]
    args.credentials.write_text("\n".join(lines) + "\n", encoding="utf-8")
    args.credentials.chmod(0o600)
    say(f"Gotowe: {len(people)} kont, 7 konkursów. Hasło i lista kont w {args.credentials}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
