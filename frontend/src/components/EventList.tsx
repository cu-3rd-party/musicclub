import { useCallback, useEffect, useMemo, useState } from "react";
import { createEvent, getEvent, listEvents, setTracklist, updateEvent } from "../services/api";
import type { PermissionSet } from "../proto/permissions_pb";
import type { Event, EventDetails } from "../proto/event_pb";
import CreateEventForm from "./forms/CreateEventForm";
import type { Timestamp } from "@bufbuild/protobuf/wkt";
import { TimestampSchema } from "@bufbuild/protobuf/wkt";
import { create } from "@bufbuild/protobuf";

type Props = {
	permissions?: PermissionSet;
};

type ListState = {
	items: Event[];
	isLoading: boolean;
	error?: Error | null;
};

const EventList = ({ permissions }: Props) => {
	const [selectedId, setSelectedId] = useState<string | null>(null);
	const [listState, setListState] = useState<ListState>({ items: [], isLoading: false, error: null });
	const [details, setDetails] = useState<EventDetails | null>(null);
	const [detailError, setDetailError] = useState<Error | null>(null);
	const [isDetailLoading, setIsDetailLoading] = useState(false);

	const fetchEvents = useCallback(async () => {
		setListState((prev) => ({ ...prev, isLoading: true, error: null }));
		try {
			const res = await listEvents();
			setListState({ items: res.events, isLoading: false, error: null });
		} catch (err) {
			setListState({ items: [], isLoading: false, error: err as Error });
		}
	}, []);

	useEffect(() => {
		fetchEvents();
	}, [fetchEvents]);

	const fetchDetails = useCallback(async (eventId: string) => {
		setIsDetailLoading(true);
		setDetailError(null);
		try {
			const res = await getEvent(eventId);
			setDetails(res);
		} catch (err) {
			setDetails(null);
			setDetailError(err as Error);
		} finally {
			setIsDetailLoading(false);
		}
	}, []);

	useEffect(() => {
		if (!selectedId) {
			setDetails(null);
			setDetailError(null);
			return;
		}
		fetchDetails(selectedId);
	}, [selectedId, fetchDetails]);

	const canEditEvents = Boolean(permissions?.events?.editEvents);
	const canEditTracklists = Boolean(permissions?.events?.editTracklists || permissions?.events?.editEvents);

	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span role="img" aria-label="calendar">
						📅
					</span>
					Мероприятия
				</div>
			</div>

			{listState.isLoading && <div>Загружаем мероприятия…</div>}
			{listState.error && <div style={{ color: "var(--danger)" }}>Ошибка: {listState.error.message}</div>}

			{listState.items.length > 0 && (
				<div className="grid">
					{listState.items.map((evt: Event) => (
						<button key={evt.id} className="button secondary" style={{ textAlign: "left" }} onClick={() => setSelectedId(evt.id)}>
							<div style={{ fontWeight: 700 }}>{evt.title}</div>
							<div style={{ color: "var(--muted)", fontSize: 13 }}>{formatDate(timestampToDate(evt.startAt as Timestamp | undefined))}</div>
							{evt.location && <div style={{ fontSize: 13 }}>{evt.location}</div>}
						</button>
					))}
				</div>
			)}

			{canEditEvents && (
				<>
					<hr style={{ border: "1px solid var(--border)", margin: "16px 0" }} />
					<CreateEventForm
						onSubmit={async (payload) => {
							await createEvent(payload);
							fetchEvents();
						}}
					/>
				</>
			)}

			{selectedId && details && !isDetailLoading && !detailError && (
				<EventDetailsCard
					data={details}
					onClose={() => setSelectedId(null)}
					onUpdate={async (payload: { title: string; startAt?: Timestamp; location?: string; notifyDayBefore?: boolean; notifyHourBefore?: boolean }) => {
						await updateEvent({ ...payload, id: selectedId });
						await fetchDetails(selectedId);
						await fetchEvents();
					}}
					onSetTracklist={
						canEditTracklists
							? async (items) => {
									await setTracklist(selectedId, items);
									await fetchDetails(selectedId);
								}
							: undefined
					}
					canEditEvents={canEditEvents}
					canEditTracklists={canEditTracklists}
				/>
			)}
		</div>
	);
};

type EventDetailsCardProps = {
	data: EventDetails;
	onClose: () => void;
	onUpdate: (payload: { title: string; startAt?: Timestamp; location?: string; notifyDayBefore?: boolean; notifyHourBefore?: boolean }) => Promise<void>;
	onSetTracklist?: (items: { order: number; songId: string; customTitle: string; customArtist: string }[]) => Promise<void>;
	canEditEvents: boolean;
	canEditTracklists: boolean;
};

const EventDetailsCard = ({ data, onClose, onUpdate, onSetTracklist, canEditEvents, canEditTracklists }: EventDetailsCardProps) => {
	const evt = data.event;
	const [form, setForm] = useState({
		title: evt?.title ?? "",
		startAt: evt?.startAt ? toInputValue(timestampToDate(evt.startAt as Timestamp)) : "",
		location: evt?.location ?? "",
		notifyDayBefore: evt?.notifyDayBefore ?? false,
		notifyHourBefore: evt?.notifyHourBefore ?? false,
	});
	const [tracklistText, setTracklistText] = useState(() =>
		(data.tracklist?.items ?? []).map((i) => `${i.order}. ${i.customTitle || i.songId || "Трек"}`).join("\n"),
	);

	const participants = useMemo(() => data.participants ?? [], [data.participants]);

	return (
		<div
			style={{
				position: "fixed",
				inset: 0,
				background: "rgba(0,0,0,0.65)",
				display: "flex",
				alignItems: "center",
				justifyContent: "center",
				padding: 16,
				zIndex: 900,
			}}
			onClick={onClose}
		>
			<div className="card" style={{ maxWidth: 720, width: "100%" }} onClick={(e) => e.stopPropagation()}>
				<div className="section-header">
					<div className="card-title">
						<span role="img" aria-label="event">
							🚀
						</span>
						{evt?.title}
					</div>
					<button className="button secondary" onClick={onClose}>
						Закрыть
					</button>
				</div>
				<div style={{ color: "var(--muted)", marginBottom: 8 }}>{formatDate(timestampToDate(evt?.startAt as Timestamp | undefined))}</div>
				{evt?.location && <div className="pill">{evt.location}</div>}

				<div style={{ marginTop: 12 }}>
					<div className="card-title" style={{ marginBottom: 6 }}>
						Участники
					</div>
					<div className="tags">
						{participants.map((p) => (
							<div key={p.role + p.user?.id} className="pill">
								{p.user?.displayName} — {p.role}
							</div>
						))}
						{participants.length === 0 && <div style={{ color: "var(--muted)" }}>Пока пусто</div>}
					</div>
				</div>

				<div style={{ marginTop: 12 }}>
					<div className="card-title" style={{ marginBottom: 6 }}>
						Треклист
					</div>
					<ol style={{ paddingLeft: 18, lineHeight: 1.4 }}>
						{data.tracklist?.items?.map((item) => (
							<li key={item.order}>
								{item.customTitle || item.songId || "Трек"} {item.customArtist ? `— ${item.customArtist}` : ""}
							</li>
						))}
						{!data.tracklist?.items?.length && <div style={{ color: "var(--muted)" }}>Не задан</div>}
					</ol>
				</div>

				{canEditEvents && (
					<form
						className="grid"
						style={{ marginTop: 12 }}
						onSubmit={(e) => {
							e.preventDefault();
							onUpdate({
								title: form.title,
								startAt: form.startAt ? toTimestamp(new Date(form.startAt)) : undefined,
								location: form.location,
								notifyDayBefore: form.notifyDayBefore,
								notifyHourBefore: form.notifyHourBefore,
							});
						}}
					>
						<div className="card-title">Редактировать</div>
						<input className="input" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
						<input
							className="input"
							type="datetime-local"
							value={form.startAt}
							onChange={(e) => setForm({ ...form, startAt: e.target.value })}
						/>
						<input className="input" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} placeholder="Локация" />
						<label style={{ display: "flex", gap: 8, alignItems: "center" }}>
							<input
								type="checkbox"
								checked={form.notifyDayBefore}
								onChange={(e) => setForm({ ...form, notifyDayBefore: e.target.checked })}
							/>
							Напомнить за день
						</label>
						<label style={{ display: "flex", gap: 8, alignItems: "center" }}>
							<input
								type="checkbox"
								checked={form.notifyHourBefore}
								onChange={(e) => setForm({ ...form, notifyHourBefore: e.target.checked })}
							/>
							Напомнить за час
						</label>
						<button className="button" type="submit">
							Сохранить
						</button>
					</form>
				)}

				{canEditTracklists && onSetTracklist && (
					<div style={{ marginTop: 12 }}>
						<div className="card-title">Обновить треклист</div>
						<textarea
							className="textarea"
							rows={5}
							value={tracklistText}
							onChange={(e) => setTracklistText(e.target.value)}
							placeholder={"1. Моя песня — Вокал\n2. Песня 2"}
						/>
						<button
							className="button"
							onClick={() => {
								const items = parseTracklist(tracklistText);
								onSetTracklist(items);
							}}
						>
							Сохранить треклист
						</button>
					</div>
				)}
			</div>
		</div>
	);
};

function formatDate(date?: Date) {
	if (!date) return "Дата не задана";
	return date.toLocaleString("ru-RU", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
}

function timestampToDate(ts?: Timestamp) {
	if (!ts) return undefined;
	// @ts-ignore seconds is bigint per generated type
	const seconds = typeof ts.seconds === "bigint" ? Number(ts.seconds) : (ts as any).seconds ?? 0;
	const nanos = (ts as any).nanos ?? 0;
	return new Date(seconds * 1000 + Math.floor(nanos / 1_000_000));
}

function toInputValue(date?: Date) {
	if (!date) return "";
	const pad = (n: number) => n.toString().padStart(2, "0");
	return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toTimestamp(date: Date): Timestamp {
	return create(TimestampSchema, {
		seconds: BigInt(Math.floor(date.getTime() / 1000)),
		nanos: date.getMilliseconds() * 1_000_000,
	});
}

function parseTracklist(text: string): { order: number; songId: string; customTitle: string; customArtist: string }[] {
	return text
		.split("\n")
		.map((line) => line.trim())
		.filter(Boolean)
		.map((line, idx) => {
			const [titlePart, artistPart] = line.split("—").map((p) => p.trim());
			return {
				order: idx + 1,
				songId: "",
				customTitle: titlePart || `Трек ${idx + 1}`,
				customArtist: artistPart || "",
			};
		});
}

export default EventList;
