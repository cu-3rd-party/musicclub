import { useMemo, useState } from "react";
import type { FormEvent } from "react";
import { createPortal } from "react-dom";
import type { SongDetails, SongLinkType } from "../proto/song_pb";

type Props = {
	details: SongDetails;
	onClose: () => void;
	onJoin: (role: string) => void;
	onLeave: (role: string) => void;
	onUpdate: (payload: {
		title: string;
		artist: string;
		description?: string;
		linkUrl: string;
		linkKind: SongLinkType;
		roles: string[];
		thumbnailUrl?: string;
		featured?: boolean;
	}) => Promise<void>;
	onDelete: () => Promise<void>;
	canEdit: boolean;
	canEditAny: boolean;
	currentUserId: string;
};

const SongModal = ({ details, onClose, onJoin, onLeave, onUpdate, onDelete, canEdit, canEditAny, currentUserId }: Props) => {
	const { song } = details;
	const [isEditing, setIsEditing] = useState(false);
	const canFeature = Boolean(details.permissions?.songs?.editFeaturedSongs);
	const [form, setForm] = useState({
		title: song?.title ?? "",
		artist: song?.artist ?? "",
		description: song?.description ?? "",
		linkUrl: song?.link?.url ?? "",
		linkKind: (song?.link?.kind ?? 0) as SongLinkType,
		roles: song?.availableRoles ?? [],
		thumbnailUrl: song?.thumbnailUrl ?? "",
		featured: song?.featured ?? false,
	});

	const assignments = details.assignments ?? [];
	const filledRoleCount = useMemo(() => new Set(assignments.map((a) => a.role)).size, [assignments]);
	const totalRoles = song?.availableRoles?.length || 0;
	const isFull = filledRoleCount >= totalRoles;

	const handleCopyMentions = async () => {
		const title = song?.title ?? "";
		const mentions = assignments
			.map((assignment) => assignment.user?.username || assignment.user?.displayName)
			.filter((value): value is string => Boolean(value));
		const uniqueMentions = Array.from(new Set(mentions));
		const mentionLines = uniqueMentions.map((name) => (name.startsWith("@") ? name : `@${name}`));
		// const quoteLines = mentionLines.map((line) => `> ${line}`).join("\n");
		const quoteLines = mentionLines.join("\n");
		const payload = `**${title}:**\n${quoteLines}`;
		try {
			await navigator.clipboard.writeText(payload);
		} catch (error) {
			console.error("Failed to copy song info", error);
		}
	};

	const linkLabel = useMemo(() => {
		const map: Record<number, string> = {
			1: "YouTube",
			2: "Я.Музыка",
			3: "Soundcloud",
		};
		return map[form.linkKind] ?? "Ссылка";
	}, [form.linkKind]);

	const handleSubmit = async (e: FormEvent) => {
		e.preventDefault();
		await onUpdate({
			title: form.title,
			artist: form.artist,
			description: form.description,
			linkUrl: form.linkUrl,
			linkKind: form.linkKind,
			roles: form.roles,
			thumbnailUrl: form.thumbnailUrl,
			featured: canFeature ? form.featured : undefined,
		});
		setIsEditing(false);
	};

	return createPortal(
		<div className="modal-backdrop" onClick={onClose}>
			<div className="card modal-window" onClick={(e) => e.stopPropagation()}>
				<div className="section-header">
					<div className="card-title">
						<span role="img" aria-label="note">
							🎶
						</span>
						{song?.title}
					</div>
					<button className="button secondary" onClick={onClose}>
						Закрыть
					</button>
				</div>
				<div className="scroll-area">
					<div style={{ color: "var(--muted)", marginBottom: 10 }}>{song?.artist}</div>
					{song?.thumbnailUrl && (
						<img
							src={song.thumbnailUrl}
							alt={song.title}
							style={{
								width: "100%",
								maxHeight: 240,
								objectFit: "cover",
								borderRadius: 8,
								marginBottom: 12
							}}
							onError={(e) => {
								e.currentTarget.style.display = "none";
							}}
						/>
					)}
					{song?.link?.url && (
						<a href={song.link.url} target="_blank" rel="noreferrer" className="pill">
							{linkLabel}
						</a>
					)}
					{song?.description && <p style={{ marginTop: 12, lineHeight: 1.5 }}>{song.description}</p>}

					<div style={{ marginTop: 14 }}>
						<div className="card-title" style={{ marginBottom: 8, display: "flex", alignItems: "center", gap: 8 }}>
							<span>Роли</span>
							<span style={{
								fontSize: 12,
								padding: "2px 8px",
								borderRadius: 4,
								backgroundColor: isFull ? "var(--danger-bg)" : "var(--accent-bg)",
								color: isFull ? "var(--danger)" : "var(--accent)"
							}}>
								{filledRoleCount}/{totalRoles}
							</span>
						</div>
						<div className="tags">
							{song?.availableRoles?.map((role: string, index: number) => {
								const members = assignments.filter((a) => a.role === role);
								const isMine = members.some((m) => m.user?.id === currentUserId);
								return (
									<div key={`${role}-${index}`} className="pill" style={{ borderColor: isMine ? "var(--accent)" : "var(--border)" }}>
										<div style={{ flex: 1 }}>
											<div style={{ fontWeight: 700 }}>{role}</div>
											<div style={{ fontSize: 12, color: "var(--muted)" }}>
												{members.length === 0 ? "Свободно" : members.map((m) => m.user?.displayName).join(", ")}
											</div>
										</div>
										{isMine ? (
											<button className="button secondary" onClick={() => onLeave(role)}>
												Снять участие
											</button>
										) : (
											<button className="button" onClick={() => onJoin(role)} disabled={isFull}>
												Присоединиться
											</button>
										)}
									</div>
								);
							})}
						</div>
					</div>

					<div style={{ marginTop: 14 }}>
						<div className="card-title" style={{ marginBottom: 8, display: "flex", alignItems: "center", gap: 10 }}>
							<span>Участники</span>
							{canEditAny && (
								<button className="button secondary" type="button" onClick={handleCopyMentions}>
									Скопировать теги
								</button>
							)}
						</div>
						<div className="grid">
							{assignments.map((a) => (
								<div key={a.role + a.user?.id} className="pill">
									{a.user?.displayName} — {a.role}
								</div>
							))}
							{assignments.length === 0 && <div style={{ color: "var(--muted)" }}>Пока пусто</div>}
						</div>
					</div>

					{canEdit && (
						<div style={{ marginTop: 16 }}>
							<button className="button secondary" onClick={() => setIsEditing((v) => !v)}>
								{isEditing ? "Скрыть форму" : "Редактировать"}
							</button>
							{isEditing && (
								<form onSubmit={handleSubmit} className="grid" style={{ marginTop: 12 }}>
									<input className="input" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="Название" required />
									<input className="input" value={form.artist} onChange={(e) => setForm({ ...form, artist: e.target.value })} placeholder="Исполнитель" required />
									<textarea
										className="textarea"
										value={form.description}
										onChange={(e) => setForm({ ...form, description: e.target.value })}
										placeholder="Описание"
									/>
									<input
										className="input"
										value={form.linkUrl}
										onChange={(e) => setForm({ ...form, linkUrl: e.target.value })}
										placeholder="Ссылка"
										required
									/>
									<select
										className="select"
										value={form.linkKind}
										onChange={(e) => setForm({ ...form, linkKind: Number(e.target.value) as SongLinkType })}
									>
										<option value={1}>YouTube</option>
										<option value={2}>Яндекс Музыка</option>
										<option value={3}>Soundcloud</option>
									</select>
									<input
										className="input"
										value={form.roles.join(", ")}
										onChange={(e) => setForm({ ...form, roles: e.target.value.split(",").map((r) => r.trim()).filter(Boolean) })}
										placeholder="Роли через запятую"
									/>
									{canFeature && (
										<label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 14 }}>
											<input
												type="checkbox"
												checked={form.featured}
												onChange={(e) => setForm({ ...form, featured: e.target.checked })}
											/>
											Featured
										</label>
									)}
									<div style={{ display: "flex", gap: 10, alignItems: "center" }}>
										<button className="button" type="submit">
											Сохранить
										</button>
										<button className="button danger" type="button" onClick={() => onDelete()}>
											Удалить песню
										</button>
									</div>
								</form>
							)}
						</div>
					)}
				</div>
			</div>
		</div>
		, document.body);
};

export default SongModal;
