import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import type { OperatorCompetition } from "@/lib/operator-competitions";
import { TemplatesSection } from "./templates-section";

function competition(template: { fileName: string; format: "Pdf"; sizeInBytes: number } | null): OperatorCompetition {
  return {
    id: "c1",
    attachments: [
      { id: "r1", title: "Oświadczenie", description: null, requirement: "Required", allowedFormats: ["Pdf"], template },
    ],
  } as unknown as OperatorCompetition;
}

function stubFetch() {
  const calls: { url: string; init?: RequestInit }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      calls.push({ url, init });
      return init?.method === "PUT"
        ? new Response(JSON.stringify({ fileName: "wzor.pdf", format: "Pdf", sizeInBytes: 10 }), { status: 200 })
        : new Response(null, { status: 204 });
    }),
  );
  return calls;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("TemplatesSection", () => {
  it("uploads the chosen file to the requirement and reloads", async () => {
    const calls = stubFetch();
    const onChanged = vi.fn();
    render(<TemplatesSection competition={competition(null)} onChanged={onChanged} />);

    const file = new File(["%PDF-1.4"], "wzor.pdf", { type: "application/pdf" });
    fireEvent.change(screen.getByLabelText(/Wgraj wzór Oświadczenie/), { target: { files: [file] } });

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    const put = calls.find((call) => call.init?.method === "PUT")!;
    expect(put.url).toContain("/competition-attachments/r1/template");
    expect((put.init!.body as FormData).get("file")).toBe(file);
  });

  it("links the template in force and withdraws it", async () => {
    const calls = stubFetch();
    const onChanged = vi.fn();
    render(
      <TemplatesSection competition={competition({ fileName: "wzor.pdf", format: "Pdf", sizeInBytes: 10 })} onChanged={onChanged} />,
    );

    expect(screen.getByRole("link", { name: "wzor.pdf" }).getAttribute("href")).toContain("/competition-attachments/r1/template");
    fireEvent.click(screen.getByRole("button", { name: "Wycofaj wzór Oświadczenie" }));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(calls.some((call) => call.url.includes("/competition-attachments/r1/template/withdraw"))).toBe(true);
  });
});
