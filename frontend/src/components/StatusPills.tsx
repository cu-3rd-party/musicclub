
export function StatusPills({ songsCount, concertsCount, participationsCount, fetching }: StatusPillsProps) {
	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					Quick status
				</div>
			</div>
			<div className="grid" style={{ gap: 8 }}>
				<div className="pill">
					Songs: {songsCount} {fetching ? "..." : ""}
				</div>
				<div className="pill">
					Concerts: {concertsCount} {fetching ? "..." : ""}
				</div>
				<div className="pill">
					Participations: {participationsCount} {fetching ? "..." : ""}
				</div>
			</div>
		</div>
	);
} export type StatusPillsProps = {
	songsCount: number;
	concertsCount: number;
	participationsCount: number;
	fetching: boolean;
};

