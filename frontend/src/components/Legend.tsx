export function Legend() {
	return (
		<div className="legend">
			<div className="legend-copy">
				<div className="pill">
					<span className="status-dot" />
					Music Club Control Room
				</div>
				<h1>Plan sets, lineups, and who plays what.</h1>
				<p className="subtle">
					Manage songs, concerts, and participations with the gRPC backend. Pick a song, join a part, and track your
					active slots.
				</p>
			</div>
			<div className="legend-visual">
				<div className="pulse" />
				<div className="pulse secondary" />
			</div>
		</div>
	);
}
