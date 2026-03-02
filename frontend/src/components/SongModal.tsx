import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { createPortal } from "react-dom";
import type { SongDetails, SongLinkType } from "../proto/song_pb";
import "../styles/components/song-modal.css";

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
		rolesText: (song?.availableRoles ?? []).join(", "),
		thumbnailUrl: song?.thumbnailUrl ?? "",
		featured: song?.featured ?? false,
	});

	const assignments = details.assignments ?? [];
	const filledRoleCount = useMemo(() => new Set(assignments.map((a) => a.role)).size, [assignments]);
	const totalRoles = song?.availableRoles?.length || 0;
	const isFull = filledRoleCount >= totalRoles;

	useEffect(() => {
		const { style } = document.body;
		const previousOverflow = style.overflow;
		style.overflow = "hidden";
		return () => {
			style.overflow = previousOverflow;
		};
	}, []);

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
		const roles = form.rolesText
			.split(",")
			.map((role) => role.trim())
			.filter(Boolean);
		await onUpdate({
			title: form.title,
			artist: form.artist,
			description: form.description,
			linkUrl: form.linkUrl,
			linkKind: form.linkKind,
			roles,
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
					<div className="song-modal__artist">{song?.artist}</div>
					{song?.thumbnailUrl && (
						<img
							src={song.thumbnailUrl}
							alt={song.title}
							className="song-modal__thumbnail"
							onError={(e) => {
								e.currentTarget.classList.add("is-hidden");
							}}
						/>
					)}
					{song?.link?.url && (
						<a href={song.link.url} target="_blank" rel="noreferrer" className="pill">
							{linkLabel}
						</a>
					)}
					{song?.description && <p className="song-modal__description">{song.description}</p>}

					<div className="song-modal__section">
						<div className="card-title song-modal__section-title">
							<span>Роли</span>
							<span className={`song-modal__role-count ${isFull ? "is-full" : "is-open"}`}>
								{filledRoleCount}/{totalRoles}
							</span>
						</div>
						<div className="tags">
							{song?.availableRoles?.map((role: string, index: number) => {
								const members = assignments.filter((a) => a.role === role);
								const isMine = members.some((m) => m.user?.id === currentUserId);
								return (
									<div key={`${role}-${index}`} className={`pill song-modal__role-pill ${isMine ? "is-mine" : ""}`}>
										<div className="song-modal__role-info">
											<div className="song-modal__role-title">{role}</div>
											<div className="song-modal__role-members">
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

					<div className="song-modal__section">
						<div className="card-title song-modal__section-title song-modal__section-title--wide">
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
							{assignments.length === 0 && <div className="song-modal__empty">Пока пусто</div>}
						</div>
					</div>

					{canEdit && (
						<div className="song-modal__edit">
							<button className="button secondary" onClick={() => setIsEditing((v) => !v)}>
								{isEditing ? "Скрыть форму" : "Редактировать"}
							</button>
							{isEditing && (
								<form onSubmit={handleSubmit} className="grid song-modal__edit-form">
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
										value={form.rolesText}
										onChange={(e) => setForm({ ...form, rolesText: e.target.value })}
										placeholder="Роли через запятую"
									/>
									{canFeature && (
										<label className="song-modal__feature-label">
											<input
												type="checkbox"
												checked={form.featured}
												onChange={(e) => setForm({ ...form, featured: e.target.checked })}
											/>
											Featured
										</label>
									)}
									<div className="song-modal__edit-actions">
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
