import type { Participation } from "../gen/participation_pb.js";
import type { Song } from "../gen/song_pb.js";

export function MyParticipationsCard({ tgId, participations, songMap }: MyParticipationsCardProps) {
	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					My participations
				</div>
				<span className="subtle">Filtered by TG id</span>
			</div>
			{tgId ? (
				participations.length ? (
					<div className="scroll-y">
						{participations.map((p: Participation) => {
							const song = songMap.get(p.songId.toString());
							return (
								<div key={`${p.tgId.toString()}-${p.songId.toString()}-${p.roleTitle}`} className="list-row">
									<div>
										<strong>{song?.title ?? "Unknown song"}</strong>
										<div className="subtle">Song #{p.songId.toString()}</div>
									</div>
									<div className="subtle">{p.roleTitle}</div>
									<div className="pill">You</div>
								</div>
							);
						})}
					</div>
				) : (
					<div className="subtle">No participations yet.</div>
				)
			) : (
				<div className="subtle">Enter your TG id above to see your active slots.</div>
			)}
		</div>
	);
}
export type MyParticipationsCardProps = {
	tgId: string;
	participations: Participation[];
	songMap: Map<string, Song>;
};

