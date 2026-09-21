import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import { CompetitionPermalink } from "./competition-permalink";

const PATH = "/competitions/5b2a0f06-1d3c-4a1e-9f1a-1f3f2b7c9d01";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function stubClipboard(writeText: () => Promise<void>) {
  vi.stubGlobal("navigator", { ...navigator, clipboard: { writeText } });
}

describe("CompetitionPermalink", () => {
  it("offers the whole address, not the path, so it can be pasted anywhere", async () => {
    render(<CompetitionPermalink path={PATH} />);

    const field = screen.getByLabelText(
      "Stały odnośnik do tego konkursu",
    ) as HTMLInputElement;

    await waitFor(() =>
      expect(field.value).toBe(`${window.location.origin}${PATH}`),
    );
  });

  it("confirms a copy out loud, because nothing else on screen changes", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);

    render(<CompetitionPermalink path={PATH} />);

    fireEvent.click(screen.getByRole("button", { name: "Kopiuj odnośnik" }));

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith(`${window.location.origin}${PATH}`),
    );
    await waitFor(() =>
      expect(screen.getByText("Skopiowano odnośnik do schowka.")).toBeTruthy(),
    );
  });

  it("survives a browser that refuses the clipboard, leaving the field to copy by hand", async () => {
    stubClipboard(vi.fn().mockRejectedValue(new Error("odmowa")));

    render(<CompetitionPermalink path={PATH} />);

    fireEvent.click(screen.getByRole("button", { name: "Kopiuj odnośnik" }));

    await waitFor(() =>
      expect(screen.getByLabelText("Stały odnośnik do tego konkursu")).toBeTruthy(),
    );
    expect(screen.queryByText("Skopiowano odnośnik do schowka.")).toBeNull();
  });
});
