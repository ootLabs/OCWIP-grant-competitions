"use client";

import { useEffect, useState } from "react";

import { fetchTeam, teamRoleLabels, type TeamAccount } from "@/lib/team-accounts";

/**
 * Zespół OCWIP (T-104): every operator and expert, with role and whether the
 * account is active. Read only on purpose: the roles are granted and the
 * accounts deactivated by the commands on the server, which the note says.
 */
export function TeamList() {
  const [team, setTeam] = useState<TeamAccount[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    fetchTeam()
      .then(setTeam)
      .catch(() => setFailed(true));
  }, []);

  return (
    <section aria-labelledby="zespol" className="flex flex-col gap-2">
      <h2 id="zespol" className="text-xl">
        Zespół OCWIP
      </h2>
      <p className="text-sm">
        Role nadaje i konta wyłącza administrator komendami na serwerze (grant-role, deactivate-account). Ta lista
        jest tylko do odczytu.
      </p>
      {failed ? <p className="text-sm">Nie udało się pobrać listy zespołu.</p> : null}
      {team === null && !failed ? <p className="text-sm">Wczytywanie zespołu…</p> : null}
      {team !== null ? (
        <table className="text-sm">
          <thead>
            <tr>
              <th scope="col" className="pr-4 text-left">Osoba</th>
              <th scope="col" className="pr-4 text-left">Adres</th>
              <th scope="col" className="pr-4 text-left">Rola</th>
              <th scope="col" className="text-left">Konto</th>
            </tr>
          </thead>
          <tbody>
            {team.map((account) => (
              <tr key={account.id}>
                <td className="pr-4">{`${account.firstName} ${account.lastName}`.trim() || "(bez nazwiska)"}</td>
                <td className="pr-4">{account.email}</td>
                <td className="pr-4">{teamRoleLabels[account.role]}</td>
                <td>{account.isActive ? "aktywne" : "wyłączone"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </section>
  );
}
