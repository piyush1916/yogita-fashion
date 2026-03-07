export default function LoadingState({ label = "Loading..." }) {
  return (
    <div className="loading-state" role="status" aria-live="polite">
      <span className="loader" />
      <span>{label}</span>
    </div>
  );
}
