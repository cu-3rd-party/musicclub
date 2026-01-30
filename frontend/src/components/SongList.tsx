import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createSong, deleteSong, getSong, joinSongRole, leaveSongRole, listSongs, updateSong } from "../services/api";
import type { PermissionSet } from "../proto/permissions_pb";
import type { Song, SongDetails, SongLinkType } from "../proto/song_pb";
import type { User } from "../proto/user_pb";
import SongModal from "./SongModal";
import CreateSongForm from "./forms/CreateSongForm";

type Props = {
	permissions?: PermissionSet;
	profile?: User;
};

type ListState = {
	items: Song[];
	nextPageToken?: string;
	isLoading: boolean;
	isFetchingNext: boolean;
	error?: Error | null;
};

const SongList = ({ permissions, profile }: Props) => {
	const [query, setQuery] = useState("");
	const [selectedId, setSelectedId] = useState<string | null>(null);
	const [showFull, setShowFull] = useState(true);
	const [showNotFull, setShowNotFull] = useState(true);
	const [isFilterOpen, setIsFilterOpen] = useState(false);
	const [listState, setListState] = useState<ListState>({
		items: [],
		nextPageToken: undefined,
		isLoading: false,
		isFetchingNext: false,
		error: null,
	});
	const nextPageTokenRef = useRef<string | undefined>(undefined);
	const [details, setDetails] = useState<SongDetails | null>(null);
	const [isDetailLoading, setIsDetailLoading] = useState(false);
	const [detailError, setDetailError] = useState<Error | null>(null);
	const wasHiddenRef = useRef(false);
	const filterMenuRef = useRef<HTMLDivElement | null>(null);

	const fetchSongs = useCallback(async (reset = false) => {
		setListState((prev) => ({
			...prev,
			isLoading: reset ? true : prev.isLoading,
			isFetchingNext: reset ? false : true,
			error: null,
		}));

		const pageToken = reset ? "" : nextPageTokenRef.current ?? "";
		if (reset) {
			nextPageTokenRef.current = undefined;
		}
		try {
			const res = await listSongs(query, pageToken);
			nextPageTokenRef.current = res.nextPageToken || undefined;
			setListState((prev) => ({
				items: reset ? res.songs : [...prev.items, ...res.songs],
				nextPageToken: nextPageTokenRef.current,
				isLoading: false,
				isFetchingNext: false,
				error: null,
			}));
		} catch (err) {
			setListState((prev) => ({
				...prev,
				isLoading: false,
				isFetchingNext: false,
				error: err as Error,
			}));
		}
	}, [query]);

	useEffect(() => {
		setListState((prev) => ({ ...prev, isLoading: true, error: null, nextPageToken: undefined }));
		fetchSongs(true);
	}, [query, fetchSongs]);

	useEffect(() => {
		const markHidden = () => {
			wasHiddenRef.current = true;
		};
		const handleFocus = () => {
			if (!wasHiddenRef.current) {
				return;
			}
			wasHiddenRef.current = false;
			fetchSongs(true);
		};
		const handleVisibility = () => {
			if (document.hidden) {
				wasHiddenRef.current = true;
				return;
			}
			handleFocus();
		};

		window.addEventListener("focus", handleFocus);
		window.addEventListener("blur", markHidden);
		document.addEventListener("visibilitychange", handleVisibility);

		return () => {
			window.removeEventListener("focus", handleFocus);
			window.removeEventListener("blur", markHidden);
			document.removeEventListener("visibilitychange", handleVisibility);
		};
	}, [fetchSongs]);

	useEffect(() => {
		if (!isFilterOpen) {
			return;
		}
		const handleClickOutside = (event: MouseEvent) => {
			if (!filterMenuRef.current) {
				return;
			}
			if (filterMenuRef.current.contains(event.target as Node)) {
				return;
			}
			setIsFilterOpen(false);
		};
		document.addEventListener("mousedown", handleClickOutside);
		return () => {
			document.removeEventListener("mousedown", handleClickOutside);
		};
	}, [isFilterOpen]);

	const fetchDetails = useCallback(async (songId: string) => {
		setIsDetailLoading(true);
		setDetailError(null);
		try {
			const res = await getSong(songId);
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

	const canCreate = Boolean(permissions?.songs?.editAnySongs || permissions?.songs?.editOwnSongs);
	const canFeature = Boolean(permissions?.songs?.editFeaturedSongs);
	const hasNextPage = Boolean(listState.nextPageToken);
	const filteredItems = useMemo(() => {
		if (showFull && showNotFull) {
			return listState.items;
		}
		return listState.items.filter((song) => {
			const totalRoles = song.availableRoles?.length || 0;
			const assignedCount = song.assignmentCount || 0;
			const isFull = assignedCount >= totalRoles;
			return (showFull && isFull) || (showNotFull && !isFull);
		});
	}, [listState.items, showFull, showNotFull]);

	return (
		<div className="card">
			<div className="section-header">
				<div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", width: "100%" }}>
					<input
						className="input"
						placeholder="Поиск по названию или исполнителю"
						value={query}
						onChange={(e) => setQuery(e.target.value)}
						style={{ flex: 1, minWidth: 240 }}
					/>
					<div className="dropdown" ref={filterMenuRef}>
						<button
							className="button secondary"
							type="button"
							aria-haspopup="true"
							aria-expanded={isFilterOpen}
							onClick={() => setIsFilterOpen((prev) => !prev)}
						>
							Фильтры
						</button>
						{isFilterOpen && (
							<div className="dropdown-menu">
								<label className="checkbox">
									<input
										className="checkbox-input"
										type="checkbox"
										checked={showFull}
										onChange={(e) => setShowFull(e.target.checked)}
									/>
									<span className="checkbox-box" aria-hidden="true" />
									<span className="checkbox-label">укомплектованные</span>
								</label>
								<label className="checkbox">
									<input
										className="checkbox-input"
										type="checkbox"
										checked={showNotFull}
										onChange={(e) => setShowNotFull(e.target.checked)}
									/>
									<span className="checkbox-box" aria-hidden="true" />
									<span className="checkbox-label">с местами</span>
								</label>
							</div>
						)}
					</div>
				</div>
			</div>

			{listState.isLoading && listState.items.length === 0 && <div>Загружаем песни…</div>}
			{listState.error && <div style={{ color: "var(--danger)" }}>Ошибка: {listState.error.message}</div>}

			{filteredItems.length > 0 && (
				<div className="grid">
					{filteredItems.map((song: Song) => (
						<SongRow key={song.id} song={song} onOpen={() => setSelectedId(song.id)} />
					))}
				</div>
			)}

			{hasNextPage && (
				<div style={{ marginTop: 12, display: "flex", justifyContent: "center" }}>
					<button
						className="button"
						onClick={() => fetchSongs(false)}
						disabled={listState.isFetchingNext}
					>
						{listState.isFetchingNext ? "Загружаем…" : "Показать еще"}
					</button>
				</div>
			)}

			{canCreate && (
				<>
					<hr style={{ border: "1px solid var(--border)", margin: "16px 0" }} />
					<CreateSongForm
						canFeature={canFeature}
						onSubmit={async (payload) => {
							await createSong(payload);
							fetchSongs(true);
						}}
					/>
				</>
			)}

			{selectedId && details && !isDetailLoading && !detailError && (
				<SongModal
					details={details}
					onClose={() => setSelectedId(null)}
					onJoin={async (role) => {
						await joinSongRole(selectedId, role);
						await fetchDetails(selectedId);
						await fetchSongs(true);
					}}
					onLeave={async (role) => {
						await leaveSongRole(selectedId, role);
						await fetchDetails(selectedId);
						await fetchSongs(true);
					}}
					onUpdate={async (payload) => {
						await updateSong({ ...payload, id: selectedId });
						await fetchDetails(selectedId);
						await fetchSongs(true);
					}}
					onDelete={async () => {
						await deleteSong(selectedId);
						setSelectedId(null);
						await fetchSongs(true);
					}}
					canEdit={Boolean(details.permissions?.songs?.editAnySongs || details.permissions?.songs?.editOwnSongs)}
					canEditAny={Boolean(details.permissions?.songs?.editAnySongs)}
					currentUserId={profile?.id ?? ""}
				/>
			)}
		</div>
	);
};

const SongRow = ({ song, onOpen }: { song: Song; onOpen: () => void }) => {
	const badge = useMemo(() => {
		const kind = song.link?.kind ?? 0;
		const map: Record<number, string> = {
			0: "ссылка",
			1: "YouTube",
			2: "Яндекс Музыка",
			3: "Soundcloud",
		};
		return map[kind as SongLinkType] ?? "ссылка";
	}, [song.link?.kind]);

	const totalRoles = song.availableRoles?.length || 0;
	const assignedCount = song.assignmentCount || 0;
	const isFull = assignedCount >= totalRoles;
	const isFeatured = Boolean(song.featured);

	return (
		<button
			className="button secondary"
			style={{
				width: "100%",
				textAlign: "left",
				background: isFeatured ? "var(--featured-bg)" : undefined,
				border: isFeatured ? "1px solid var(--featured-border)" : undefined,
			}}
			onClick={onOpen}
		>
			<div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" }}>
				{song.thumbnailUrl && (
					<img
						src={song.thumbnailUrl}
						alt={song.title}
						style={{
							width: 80,
							height: 60,
							objectFit: "cover",
							borderRadius: 4,
							flexShrink: 0
						}}
						onError={(e) => {
							// Fallback: hide image if it fails to load
							e.currentTarget.style.display = "none";
						}}
					/>
				)}
				<div style={{ flex: 1, minWidth: 0 }}>
					<div style={{ fontWeight: 700, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{song.title}</div>
					<div style={{ color: "var(--muted)", fontSize: 14, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{song.artist}</div>
				</div>
				<div style={{ display: "flex", gap: 6, alignItems: "center", flexShrink: 0 }}>
					<span style={{
						fontSize: 18,
						padding: "2px 6px",
						borderRadius: 4,
						backgroundColor: isFull ? "var(--danger-bg)" : "var(--accent-bg)",
						color: isFull ? "var(--danger)" : "var(--accent)",
						fontWeight: 600
					}}>
						{assignedCount}/{totalRoles}
					</span>
				</div>
			</div>
		</button>
	);
};

export default SongList;
