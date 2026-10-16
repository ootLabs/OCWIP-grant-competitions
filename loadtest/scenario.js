// T-118: the intake as it went in 2026. 149 applications, most of them in the
// last hours. Every applicant is one virtual user who signs in, starts a
// draft, saves it every 30 seconds, uploads a 5 MB statute and submits; most
// submit in the last tenth of the window (10 of 120 minutes).
//
// Goals (docs/runbook/plan-v1.md, T-118):
//   p95 of the autosave under 1 s, no 5xx at all, no submission refused
//   before the deadline.
//
// Run (docs/wdrozenie.md, "Test obciążenia"):
//   k6 run -e STATE=loadtest/state.json -e BASE_URL=https://ocwip.test \
//          -e WINDOW_MINUTES=120 loadtest/scenario.js
//
// The window can be shortened for a rehearsal (WINDOW_MINUTES=12). The save
// interval stays 30 s, so a short window is a denser load than the real one.
import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";

const BASE = (__ENV.BASE_URL || "https://ocwip.test").replace(/\/$/, "");
const WINDOW_S = Number(__ENV.WINDOW_MINUTES || 120) * 60;
const SAVE_EVERY_S = Number(__ENV.SAVE_EVERY_S || 30);
const UPLOAD_BYTES = Number(__ENV.UPLOAD_MB || 5) * 1024 * 1024;
const LATE_SHARE = Number(__ENV.LATE_SHARE || 0.8);
const state = JSON.parse(open(__ENV.STATE || "state.json"));

export const options = {
  insecureSkipTLSVerify: __ENV.INSECURE === "1",
  hosts: __ENV.HOST_IP ? { [BASE.replace(/^https?:\/\//, "").replace(/[:\/].*$/, "")]: __ENV.HOST_IP } : {},
  scenarios: {
    intake: {
      executor: "per-vu-iterations",
      vus: state.accounts.length,
      iterations: 1,
      maxDuration: `${Math.ceil(WINDOW_S / 60) + 10}m`,
    },
  },
  thresholds: {
    "http_req_duration{name:autosave}": ["p(95)<1000"],
    server_errors: ["count==0"],
    submissions_refused: ["count==0"],
    submissions_accepted: [`count==${state.accounts.length}`],
  },
};

const serverErrors = new Counter("server_errors");
const submissionsRefused = new Counter("submissions_refused");
const submissionsAccepted = new Counter("submissions_accepted");
const submitTime = new Trend("submit_time", true);
const uploadTime = new Trend("upload_time", true);

// A PDF-looking file of the size the card asks for, built once per VU.
const statute = (() => {
  const bytes = new Uint8Array(UPLOAD_BYTES);
  const head = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n";
  for (let i = 0; i < head.length; i++) bytes[i] = head.charCodeAt(i);
  const tail = "\n%%EOF\n";
  for (let i = 0; i < tail.length; i++) bytes[bytes.length - tail.length + i] = tail.charCodeAt(i);
  return bytes.buffer;
})();

const cost = (nazwa, liczba, cena) => ({ nazwa, jednostka: "szt.", liczba, cena });

// The same complete answers the browser scenario submits (e2e/fixtures/answers-2026.ts).
function answers(title, note) {
  return {
    rodzaj_wnioskodawcy: "Organisation",
    gmina_realizacji: "Lewin Brzeski",
    charakterystyka_wnioskodawcy: "Działamy od dwóch lat na rzecz sąsiadów.",
    tytul_projektu: title,
    data_zakonczenia: "2026-11-30",
    charakterystyka_projektu: "Zakładamy ogród i uczymy uprawy warzyw.",
    cel_glowny: "Zbliżyć sąsiadów przez wspólną pracę.",
    opis_pomyslu: "o".repeat(1000),
    opis_dzialan: `Przygotowanie terenu, sadzenie, warsztaty, festyn. ${note}`,
    promocja: "Plakaty w szkole i profil w mediach społecznościowych.",
    rezultaty_opis: "Ogród zostaje pod opieką rady rodziców.",
    liczba_uczestnikow: 40,
    liczba_uczestnikow_monitorowanie: "Listy obecności",
    rezultaty: [{ rezultat: "Promocja", wartosc_docelowa: "10 plakatów", monitorowanie: "Zdjęcia" }],
    dostepnosc_architektoniczna: "Teren bez progów.",
    dostepnosc_cyfrowa: "Ogłoszenia z tekstem alternatywnym.",
    dostepnosc_informacyjna: "Informacja w tekście łatwym do czytania.",
    koszty_bezposrednie: [cost("Sadzonki", 1, 5000)],
    koszty_promocji: [cost("Plakaty", 10, 20)],
    koszty_posrednie: [cost("Księgowość", 1, 500)],
    o_zwiazanie: true,
    o_zgodnosc: true,
    o_niekaralnosc: true,
    o_pozytek: true,
    o_regulamin: true,
    o_rodo: true,
    o_rodo_osoby_trzecie: true,
    rodzaj_organizacji: "mloda",
    data_wpisu: "2024-03-01",
    przychod: 18000,
    o_siedziba: true,
    o_podatki: true,
    o_skladki: true,
  };
}

const json = { headers: { "Content-Type": "application/json", "X-Turnstile-Token": "XXXX.DUMMY.TOKEN.XXXX" } };

function counted(response) {
  if (response.status >= 500) serverErrors.add(1);
  return response;
}

export default function () {
  const index = __VU - 1;
  const account = state.accounts[index];
  const lateStart = WINDOW_S - WINDOW_S / 12;
  // Joins somewhere in the first nine tenths; a late submitter submits in the last twelfth.
  const joinAt = Math.random() * (WINDOW_S * 0.9);
  const submitAt =
    Math.random() < LATE_SHARE
      ? lateStart + Math.random() * (WINDOW_S / 12 - 45)
      : Math.min(lateStart, joinAt + 60 + Math.random() * (lateStart - joinAt));
  const t0 = Date.now() / 1000;
  const elapsed = () => Date.now() / 1000 - t0;

  sleep(joinAt);

  const login = counted(http.post(`${BASE}/api/login`, JSON.stringify({ email: account.email, password: account.password }), { ...json, tags: { name: "login" } }));
  if (!check(login, { "login 200": (r) => r.status === 200 })) return;

  const draft = counted(http.post(`${BASE}/api/competitions/${state.competitionId}/applications`, null, { tags: { name: "start-draft" } }));
  if (!check(draft, { "draft 2xx": (r) => r.status >= 200 && r.status < 300 })) return;
  const id = draft.json("id");

  let saves = 0;
  let uploaded = false;
  while (elapsed() < submitAt) {
    const note = `zapis ${saves}`;
    const save = counted(
      http.put(`${BASE}/api/applications/${id}`, JSON.stringify({ answers: answers(`Ogród sąsiedzki ${index}`, note) }), { ...json, tags: { name: "autosave" } }),
    );
    check(save, { "autosave 200": (r) => r.status === 200 });
    saves++;

    if (!uploaded && elapsed() > joinAt + 30) {
      const started = Date.now();
      const upload = counted(
        http.post(
          `${BASE}/api/applications/${id}/attachments`,
          { file: http.file(statute, "statut.pdf", "application/pdf"), requirementId: state.requirementId },
          { tags: { name: "upload" }, timeout: "120s" },
        ),
      );
      uploadTime.add(Date.now() - started);
      uploaded = check(upload, { "upload 2xx": (r) => r.status >= 200 && r.status < 300 });
    }
    sleep(SAVE_EVERY_S);
  }

  if (!uploaded) {
    const upload = counted(
      http.post(
        `${BASE}/api/applications/${id}/attachments`,
        { file: http.file(statute, "statut.pdf", "application/pdf"), requirementId: state.requirementId },
        { tags: { name: "upload" }, timeout: "120s" },
      ),
    );
    check(upload, { "upload 2xx": (r) => r.status >= 200 && r.status < 300 });
  }

  // One last save with the final text, then the submission.
  counted(http.put(`${BASE}/api/applications/${id}`, JSON.stringify({ answers: answers(`Ogród sąsiedzki ${index}`, "final") }), { ...json, tags: { name: "autosave" } }));
  const submit = counted(http.post(`${BASE}/api/applications/${id}/submit`, null, { tags: { name: "submit" } }));
  submitTime.add(submit.timings.duration);
  if (submit.status >= 200 && submit.status < 300) {
    submissionsAccepted.add(1);
  } else {
    submissionsRefused.add(1);
    console.error(`submit refused for ${account.email}: ${submit.status} ${String(submit.body).slice(0, 200)}`);
  }
}
