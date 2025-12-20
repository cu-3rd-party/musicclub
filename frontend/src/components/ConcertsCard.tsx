import { useEffect, useState } from "react";
import { formatTimestamp } from "./formatTimestamp.js";
import { toDateInput } from "./toDateInput.js";
import type { Concert } from "../gen/concert_pb.js";

export function ConcertsCard({ concerts, isFetching, onSave, onDelete, saving, deleting, isAdmin }: ConcertsCardProps) {
	const [concertForm, setConcertForm] = useState({ name: "", date: "" });
	const [editingConcertId, setEditingConcertId] = useState<bigint | null>(null);

	useEffect(() => {
		if (!isAdmin) {
			setEditingConcertId(null);
			setConcertForm({ name: "", date: "" });
		}
	}, [isAdmin]);

	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span className="status-dot" />
					Concerts
				</div>
				<span className="subtle">{editingConcertId ? "Editing concert" : "Create new concert"}</span>
			</div>
			{
				isAdmin ?
					<form
						className="grid"
						style={{ gap: 10 }}
						onSubmit={(e) => {
							e.preventDefault();
							const payload: { id?: bigint; name: string; date: string; } = {
								name: concertForm.name,
								date: concertForm.date,
							};
							if (editingConcertId != null) {
								payload.id = editingConcertId;
							}
							onSave(payload);
							if (!saving) {
								setEditingConcertId(null);
								setConcertForm({ name: "", date: "" });
							}
						}}
					>
						<input
							className="input"
							placeholder="Concert name"
							required
							value={concertForm.name}
							onChange={(e) => setConcertForm((prev) => ({ ...prev, name: e.target.value }))} />
						<input
							className="input"
							type="datetime-local"
							value={concertForm.date}
							onChange={(e) => setConcertForm((prev) => ({ ...prev, date: e.target.value }))} />
						<div style={{ display: "flex", gap: 10 }}>
							<button className="button" type="submit" disabled={saving}>
								{editingConcertId ? "Update concert" : "Create concert"}
							</button>
							{editingConcertId ? (
								<button
									type="button"
									className="button secondary"
									onClick={() => {
										setEditingConcertId(null);
										setConcertForm({ name: "", date: "" });
									}}
								>
									Cancel edit
								</button>
							) : null}
						</div>
					</form>
					: null
			}
			<div className="scroll-y" style={{ marginTop: 12 }}>
				{isFetching && !concerts.length ? <div className="subtle">Loading concerts...</div> : null}
				{concerts.map((concert) => (
					<div key={concert.id.toString()} className="list-row">
						<div>
							<strong>{concert.name}</strong>
							<div className="subtle">#{concert.id.toString()}</div>
						</div>
						<div className="subtle">{formatTimestamp(concert.date)}</div>
						{
							isAdmin ?
								<div style={{ display: "flex", gap: 8 }}>
									<button
										className="button secondary"
										onClick={() => {
											setEditingConcertId(concert.id);
											setConcertForm({ name: concert.name, date: toDateInput(concert.date) });
										}}
									>
										Edit
									</button>
									<button className="button danger" onClick={() => onDelete(concert)} disabled={deleting}>
										Delete
									</button>
								</div>
								: null
						}
					</div>
				))}
			</div>
		</div>
	);
} export type ConcertsCardProps = {
	concerts: Concert[];
	isFetching: boolean;
	onSave: (payload: { id?: bigint; name: string; date: string; }) => void;
	onDelete: (concert: Concert) => void;
	saving: boolean;
	deleting: boolean;
	isAdmin: boolean;
};
