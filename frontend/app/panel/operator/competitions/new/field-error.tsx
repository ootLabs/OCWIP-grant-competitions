/** One field's backend validation messages, shown right under its input. */
export function FieldError({ messages }: { messages?: string[] }) {
  if (messages === undefined || messages.length === 0) {
    return null;
  }

  return (
    <p role="alert" className="text-sm text-brand-accent-text">
      {messages.join(" ")}
    </p>
  );
}
