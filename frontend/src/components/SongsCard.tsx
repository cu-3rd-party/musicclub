import { useState } from "react";
import type { Song } from "../gen/song_pb.js";

export function SongsCard({ songs, isFetching, onSave, onDelete, saving, deleting }: SongsCardProps) {
	const [songForm, setSongForm] = useState({ title: "", description: "", link: "" });
	const [editingSongId, setEditingSongId] = useState<bigint | null>(null);

	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					Songs
				</div>
				<span className="subtle">{editingSongId ? "Editing song" : "Create new song"}</span>
			</div>
			<form
				className="grid"
				style={{ gap: 10 }}
				onSubmit={(e) => {
					e.preventDefault();
					const payload: { id?: bigint; title: string; description: string; link: string; } = {
						title: songForm.title,
						description: songForm.description,
						link: songForm.link,
					};
					if (editingSongId != null) {
						payload.id = editingSongId;
					}
					onSave(payload);
					if (!saving) {
						setEditingSongId(null);
						setSongForm({ title: "", description: "", link: "" });
					}
				}}
			>
				<input
					className="input"
					placeholder="Title"
					required
					value={songForm.title}
					onChange={(e) => setSongForm((prev) => ({ ...prev, title: e.target.value }))} />
				<textarea
					className="textarea"
					placeholder="Description"
					required
					value={songForm.description}
					onChange={(e) => setSongForm((prev) => ({ ...prev, description: e.target.value }))} />
				<input
					className="input"
					placeholder="Link to reference/recording"
					required
					value={songForm.link}
					onChange={(e) => setSongForm((prev) => ({ ...prev, link: e.target.value }))} />
				<div style={{ display: "flex", gap: 10 }}>
					<button className="button" type="submit" disabled={saving}>
						{editingSongId ? "Update song" : "Create song"}
					</button>
					{editingSongId ? (
						<button
							type="button"
							className="button secondary"
							onClick={() => {
								setEditingSongId(null);
								setSongForm({ title: "", description: "", link: "" });
							}}
						>
							Cancel edit
						</button>
					) : null}
				</div>
			</form>
			<div className="scroll-y" style={{ marginTop: 12 }}>
				{isFetching && !songs.length ? <div className="subtle">Loading songs...</div> : null}
				{songs.map((song) => (
					<div key={song.id.toString()} className="list-row">
						<div>
							<strong>{song.title}</strong>
							<div className="subtle">#{song.id.toString()}</div>
						</div>
						<div>
							<div>{song.description}</div>
							<a className="subtle" href={song.link} target="_blank" rel="noreferrer">link</a>
						</div>
						<div style={{ display: "flex", gap: 8 }}>
							<button
								className="button secondary"
								onClick={() => {
									setEditingSongId(song.id);
									setSongForm({ title: song.title, description: song.description, link: song.link });
								}}
							>
								Edit
							</button>
							<button className="button danger" onClick={() => onDelete(song)} disabled={deleting}>
								Delete
							</button>
						</div>
					</div>
				))}
			</div>
		</div>
	);
} export type SongsCardProps = {
	songs: Song[];
	isFetching: boolean;
	onSave: (payload: { id?: bigint; title: string; description: string; link: string; }) => void;
	onDelete: (song: Song) => void;
	saving: boolean;
	deleting: boolean;
};

