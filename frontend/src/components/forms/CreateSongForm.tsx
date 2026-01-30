import { useState } from "react";
import type { FormEvent } from "react";
import type { SongLinkType } from "../../proto/song_pb";

type Props = {
	onSubmit: (payload: {
		title: string;
		artist: string;
		description?: string;
		linkUrl: string;
		linkKind: SongLinkType;
		roles: string[];
		thumbnailUrl?: string;
		featured?: boolean;
	}) => Promise<void>;
	canFeature?: boolean;
};

const CreateSongForm = ({ onSubmit, canFeature }: Props) => {
	const [form, setForm] = useState({
		title: "",
		artist: "",
		description: "",
		linkUrl: "",
		linkKind: 1 as SongLinkType,
		roles: "вокал, гитара, бас, барабаны",
		thumbnailUrl: "",
		featured: false,
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
				artist: form.artist,
				description: form.description,
				linkUrl: form.linkUrl,
				linkKind: form.linkKind,
				roles: form.roles.split(",").map((r) => r.trim()).filter(Boolean),
				thumbnailUrl: form.thumbnailUrl,
				featured: canFeature ? form.featured : undefined,
			});
			setForm({ ...form, title: "", artist: "", description: "", linkUrl: "", thumbnailUrl: "", featured: false });
		} catch (err) {
			setError((err as Error).message);
		} finally {
			setIsSaving(false);
		}
	};

	return (
		<form className="grid" style={{ gap: 10 }} onSubmit={handleSubmit}>
			<div className="card-title">Добавить песню</div>
			<input className="input" placeholder="Название" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
			<input className="input" placeholder="Исполнитель" value={form.artist} onChange={(e) => setForm({ ...form, artist: e.target.value })} required />
			<textarea
				className="textarea"
				placeholder="Описание"
				value={form.description}
				onChange={(e) => setForm({ ...form, description: e.target.value })}
			/>
			<div style={{ display: "grid", gap: 8, gridTemplateColumns: "1fr 180px" }}>
				<input className="input" placeholder="Ссылка" value={form.linkUrl} onChange={(e) => setForm({ ...form, linkUrl: e.target.value })} required />
				<select
					className="select"
					value={form.linkKind}
					onChange={(e) => setForm({ ...form, linkKind: Number(e.target.value) as SongLinkType })}
				>
					<option value={1}>YouTube</option>
					<option value={2}>Яндекс Музыка</option>
					<option value={3}>Soundcloud</option>
				</select>
			</div>
			<input
				className="input"
				placeholder="Роли через запятую"
				value={form.roles}
				onChange={(e) => setForm({ ...form, roles: e.target.value })}
			/>
			{canFeature && (
				<label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 14 }}>
					<input
						type="checkbox"
						checked={form.featured}
						onChange={(e) => setForm({ ...form, featured: e.target.checked })}
					/>
					Сделать featured
				</label>
			)}
			<button className="button" type="submit" disabled={isSaving}>
				{isSaving ? "Сохраняем…" : "Создать"}
			</button>
			{error && <div style={{ color: "var(--danger)" }}>{error}</div>}
		</form>
	);
};

export default CreateSongForm;
