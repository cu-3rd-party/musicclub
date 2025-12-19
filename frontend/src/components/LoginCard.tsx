export function LoginCard({ tgIdInput, setTgIdInput, loginStatus, onLogin, loading, debugLines = [] }: LoginCardProps) {
	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					Login with TG id
				</div>
				<span className="subtle">Required for secured calls</span>
			</div>
			<form
				className="grid"
				style={{ gap: 10 }}
				onSubmit={(e) => {
					e.preventDefault();
					if (!tgIdInput.trim()) return;
					onLogin(tgIdInput.trim());
				}}
			>
				<input
					className="input"
					placeholder="Telegram user id"
					value={tgIdInput}
					onChange={(e) => setTgIdInput(e.target.value.replace(/[^0-9]/g, ""))} />
				<div style={{ display: "flex", gap: 10, alignItems: "center" }}>
					<button className="button" type="submit" disabled={loading || !tgIdInput}>
						{loading ? "Authorizing..." : "Login & set token"}
					</button>
					{loginStatus ? <span className="subtle">{loginStatus}</span> : null}
				</div>
			</form>
			{debugLines.length ? (
				<div className="subtle" style={{ marginTop: 8, fontSize: 12, lineHeight: 1.5 }}>
					<div style={{ fontWeight: 600 }}>Debug</div>
					<div className="scroll-y" style={{ maxHeight: 100 }}>
						<ul style={{ paddingLeft: 16, margin: "4px 0 0" }}>
							{debugLines.map((line, idx) => (
								<li key={idx}>{line}</li>
							))}
						</ul>
					</div>
				</div>
			) : null}
		</div>
	);
} export type LoginCardProps = {
	tgIdInput: string;
	setTgIdInput: (val: string) => void;
	loginStatus: string | null;
	onLogin: (tg: string) => void;
	loading: boolean;
	debugLines?: string[];
};
