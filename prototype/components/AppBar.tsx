type AppBarProps = {
  /** What the applicant is currently inside: the competition or a section name. */
  context: string;
  /** Who is logged in, or undefined on screens seen before logging in. */
  account?: string;
  compact?: boolean;
};

export function AppBar({ context, account, compact = false }: AppBarProps) {
  return (
    <header
      className={`flex items-center gap-4 border-b border-border ${
        compact ? "px-4 py-3" : "px-7 py-3.5"
      }`}
    >
      {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
      <img
        src="/ocwip-logo.svg"
        alt="Logo OCWIP"
        className={compact ? "h-[22px] w-auto" : "h-[26px] w-auto"}
      />
      {!compact && (
        <>
          <span className="h-[22px] w-px bg-border" aria-hidden />
          <span className="text-[13px] font-semibold">{context}</span>
        </>
      )}
      <span className="grow" />
      {account && <span className="text-[13px] text-text-link">{account}</span>}
    </header>
  );
}
