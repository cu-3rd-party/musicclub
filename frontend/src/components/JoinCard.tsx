import { useState } from "react";
import type { Song } from "../gen/song_pb.js";

export function JoinCard({ songs, tgId, onJoin, saving }: JoinCardProps) {
	const [joinForm, setJoinForm] = useState({ songId: "", roleTitle: "" });

	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					Join a song
				</div>
				<span className="subtle">Pick role and song</span>
			</div>
			<form
				className="grid"
				style={{ gap: 10 }}
				onSubmit={(e) => {
					e.preventDefault();
					if (!tgId || !joinForm.songId || !joinForm.roleTitle) return;
					onJoin({
						tgId,
						songId: joinForm.songId,
						roleTitle: joinForm.roleTitle,
					});
					if (!saving) {
						setJoinForm({ songId: "", roleTitle: "" });
					}
				}}
			>
				<select
					className="select"
					value={joinForm.songId}
					required
					onChange={(e) => setJoinForm((prev) => ({ ...prev, songId: e.target.value }))}
				>
					<option value="">Choose song</option>
					{songs.map((song) => (
						<option key={song.id.toString()} value={song.id.toString()}>
							{song.title}
						</option>
					))}
				</select>
				<input
					className="input"
					placeholder="Role title (e.g. Guitar, Vocals)"
					required
					value={joinForm.roleTitle}
					onChange={(e) => setJoinForm((prev) => ({ ...prev, roleTitle: e.target.value }))} />
				<div style={{ display: "flex", gap: 10, alignItems: "center" }}>
					<button className="button" type="submit" disabled={saving || !tgId}>
						{saving ? "Joining..." : "Join song"}
					</button>
					{!tgId ? <span className="subtle">Provide your TG id to join</span> : null}
				</div>
			</form>
		</div>
	);
} export type JoinCardProps = {
	songs: Song[];
	tgId: string;
	onJoin: (payload: { tgId: string; songId: string; roleTitle: string; }) => void;
	saving: boolean;
};

