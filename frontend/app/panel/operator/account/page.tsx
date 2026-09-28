import { AccountSettings } from "@/components/account/account-settings";

/** "Moje konto" (T-106): password and address, the same screen for every role. */
export default function AccountPage() {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Moje konto</h1>
      <AccountSettings />
    </section>
  );
}
