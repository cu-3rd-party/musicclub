import { useState } from "react";
import type { FormEvent } from "react";
import { create } from "@bufbuild/protobuf";
import { TimestampSchema } from "@bufbuild/protobuf/wkt";
import type { Timestamp } from "@bufbuild/protobuf/wkt";

type Props = {
	onSubmit: (payload: {
		title: string;
		startAt?: Timestamp;
		location?: string;
		notifyDayBefore?: boolean;
		notifyHourBefore?: boolean;
		tracklist?: { order: number; songId: string; customTitle: string; customArtist: string }[];
	}) => Promise<void>;
};

const CreateEventForm = ({ onSubmit }: Props) => {
	const [form, setForm] = useState({
		title: "",
		startAt: "",
		location: "",
		notifyDayBefore: false,
		notifyHourBefore: false,
		tracklistText: "",
	});
	const [isSaving, setIsSaving] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const handleSubmit = async (e: FormEvent) => {
		e.preventDefault();
		setIsSaving(true);
		setError(null);
		try {
			await onSubmit({
				title: form.title,
				startAt: form.startAt ? toTimestamp(new Date(form.startAt)) : undefined,
				location: form.location,
				notifyDayBefore: form.notifyDayBefore,
				notifyHourBefore: form.notifyHourBefore,
				tracklist: form.tracklistText
					? form.tracklistText
						.split("\n")
						.map((l) => l.trim())
						.filter(Boolean)
						.map((line, idx) => {
							const [titlePart, artistPart] = line.split("—").map((p) => p.trim());
							return {
								order: idx + 1,
								songId: "",
								customTitle: titlePart || `Трек ${idx + 1}`,
								customArtist: artistPart || "",
							};
						})
					: undefined,
			});
			setForm({ title: "", startAt: "", location: "", notifyDayBefore: false, notifyHourBefore: false, tracklistText: "" });
		} catch (err) {
			setError((err as Error).message);
		} finally {
			setIsSaving(false);
		}
	};

	return (
		<form className="grid" style={{ gap: 10 }} onSubmit={handleSubmit}>
			<div className="card-title">Создать мероприятие</div>
			<input className="input" placeholder="Название" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
			<input
				className="input"
				type="datetime-local"
				value={form.startAt}
				onChange={(e) => setForm({ ...form, startAt: e.target.value })}
				placeholder="Дата/время"
			/>
			<input className="input" placeholder="Локация (опционально)" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} />
			<label style={{ display: "flex", gap: 8, alignItems: "center" }}>
				<input type="checkbox" checked={form.notifyDayBefore} onChange={(e) => setForm({ ...form, notifyDayBefore: e.target.checked })} />
				Напомнить за день
			</label>
			<label style={{ display: "flex", gap: 8, alignItems: "center" }}>
				<input type="checkbox" checked={form.notifyHourBefore} onChange={(e) => setForm({ ...form, notifyHourBefore: e.target.checked })} />
				Напомнить за час
			</label>
			<textarea
				className="textarea"
				rows={4}
				placeholder={"1. Название — Исполнитель\n2. Следующий трек"}
				value={form.tracklistText}
				onChange={(e) => setForm({ ...form, tracklistText: e.target.value })}
			/>
			<button className="button" type="submit" disabled={isSaving}>
				{isSaving ? "Создаем…" : "Создать"}
			</button>
			{error && <div style={{ color: "var(--danger)" }}>{error}</div>}
		</form>
	);
};

function toTimestamp(date: Date) {
	return create(TimestampSchema, {
		seconds: BigInt(Math.floor(date.getTime() / 1000)),
		nanos: date.getMilliseconds() * 1_000_000,
	});
}

export default CreateEventForm;
