import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import type { FormDocument } from "@/lib/forms/document-types";
import { ReturnPanel } from "./return-panel";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    { key: "dane", title: "Dane projektu", description: "", fields: [] },
    { key: "budzet", title: "Budżet", description: "", fields: [] },
  ],
};

const empty = { applicationId: "a1", status: "Submitted", returns: [], versions: [], history: [] };

function stubFetch(corrections: unknown = empty) {
  const calls: { url: string; init?: RequestInit }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      calls.push({ url, init });
      if (init?.method === "POST") {
        return new Response(JSON.stringify({ id: "r1" }), { status: 201 });
      }
      return new Response(JSON.stringify(corrections), { status: 200 });
    }),
  );
  return calls;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ReturnPanel", () => {
  it("returns a submitted application with the chosen sections, the note and a Polish deadline in UTC", async () => {
    const calls = stubFetch();
    const onReturned = vi.fn();
    render(<ReturnPanel applicationId="a1" status="Submitted" document={document} onReturned={onReturned} />);

    fireEvent.click(screen.getByLabelText("Budżet"));
    fireEvent.change(screen.getByLabelText("Co poprawić (trafi do wnioskodawcy w mailu)"), {
      target: { value: "Popraw kwotę." },
    });
    fireEvent.change(screen.getByLabelText("Termin poprawy"), { target: { value: "2026-10-05T12:00" } });
    fireEvent.click(screen.getByRole("button", { name: "Zwróć do poprawy" }));

    await waitFor(() => expect(onReturned).toHaveBeenCalled());
    const post = calls.find((call) => call.init?.method === "POST")!;
    expect(post.url).toContain("/applications/a1/return");
    expect(JSON.parse(String(post.init!.body))).toEqual({
      sections: ["budzet"],
      unlocksAttachments: false,
      message: "Popraw kwotę.",
      // October, before the change: Warsaw is UTC+2.
      deadline: "2026-10-05T10:00:00.000Z",
    });
  });

  it("offers no return for an application that is not submitted, and shows the open one", async () => {
    stubFetch({
      ...empty,
      status: "Returned",
      returns: [
        {
          id: "r1",
          sections: ["budzet"],
          unlocksAttachments: true,
          message: "Popraw kwotę.",
          deadline: "2026-10-05T10:00:00Z",
          returnedAt: "2026-10-01T10:00:00Z",
          resolvedAt: null,
        },
      ],
    });
    render(<ReturnPanel applicationId="a1" status="Returned" document={document} onReturned={vi.fn()} />);

    expect(await screen.findByText(/Wniosek czeka na poprawkę do/)).toBeTruthy();
    expect(screen.getByText(/Budżet, załączniki/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Zwróć do poprawy" })).toBeNull();
  });
});
