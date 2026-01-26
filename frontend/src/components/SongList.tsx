import React, { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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

const SongList: React.FC<Props> = ({ permissions, profile }) => {
	const queryClient = useQueryClient();
	const [query, setQuery] = useState("");
	const [selectedId, setSelectedId] = useState<string | null>(null);

	const listQuery = useQuery({
		queryKey: ["songs", query],
		queryFn: () => listSongs(query),
	});

	const detailQuery = useQuery<SongDetails | null>({
		queryKey: ["song", selectedId],
		queryFn: () => (selectedId ? getSong(selectedId) : Promise.resolve(null)),
		enabled: Boolean(selectedId),
	});

	const joinMutation = useMutation<SongDetails, Error, { songId: string; role: string }>({
		mutationFn: ({ songId, role }) => joinSongRole(songId, role),
		onSuccess: (data) => {
			if (data?.song?.id) {
				queryClient.invalidateQueries({ queryKey: ["songs"] });
				queryClient.setQueryData(["song", data.song.id], data);
			}
		},
	});

	const leaveMutation = useMutation<SongDetails, Error, { songId: string; role: string }>({
		mutationFn: ({ songId, role }) => leaveSongRole(songId, role),
		onSuccess: (data) => {
			if (data?.song?.id) {
				queryClient.invalidateQueries({ queryKey: ["songs"] });
				queryClient.setQueryData(["song", data.song.id], data);
			}
		},
	});

	const canCreate = Boolean(permissions?.songs?.editAnySongs || permissions?.songs?.editOwnSongs);
	const canFeature = Boolean(permissions?.songs?.editFeaturedSongs);

	return (
		<div className="card">
			<div className="section-header">
				<div className="card-title">
					<span role="img" aria-label="song">
						🎵
					</span>
					Песни
				</div>
				<input
					className="input"
					placeholder="Поиск по названию или исполнителю"
					value={query}
					onChange={(e) => setQuery(e.target.value)}
					style={{ maxWidth: 280 }}
				/>
			</div>

			{listQuery.isLoading && <div>Загружаем песни…</div>}
			{listQuery.isError && <div style={{ color: "var(--danger)" }}>Ошибка: {(listQuery.error as Error).message}</div>}

			{listQuery.data && (
				<div className="grid">
					{listQuery.data.songs.map((song: Song) => (
						<SongRow key={song.id} song={song} onOpen={() => setSelectedId(song.id)} />
					))}
				</div>
			)}

			{canCreate && (
				<>
					<hr style={{ border: "1px solid var(--border)", margin: "16px 0" }} />
					<CreateSongForm
						canFeature={canFeature}
						onSubmit={async (payload) => {
							await createSong(payload);
							queryClient.invalidateQueries({ queryKey: ["songs"] });
						}}
					/>
				</>
			)}

			{selectedId && detailQuery.data && (
				<SongModal
					details={detailQuery.data}
					onClose={() => setSelectedId(null)}
					onJoin={(role) => joinMutation.mutate({ songId: selectedId, role })}
					onLeave={(role) => leaveMutation.mutate({ songId: selectedId, role })}
					onUpdate={async (payload) => {
						await updateSong({ ...payload, id: selectedId });
						queryClient.invalidateQueries({ queryKey: ["song", selectedId] });
						queryClient.invalidateQueries({ queryKey: ["songs"] });
					}}
					onDelete={async () => {
						await deleteSong(selectedId);
						setSelectedId(null);
						queryClient.invalidateQueries({ queryKey: ["songs"] });
					}}
					canEdit={Boolean(detailQuery.data.permissions?.songs?.editAnySongs || detailQuery.data.permissions?.songs?.editOwnSongs)}
					currentUserId={profile?.id ?? ""}
				/>
			)}
		</div>
	);
};

const SongRow: React.FC<{ song: Song; onOpen: () => void }> = ({ song, onOpen }) => {
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
						fontSize: 12,
						padding: "2px 6px",
						borderRadius: 4,
						backgroundColor: isFull ? "var(--danger-bg)" : "var(--accent-bg)",
						color: isFull ? "var(--danger)" : "var(--accent)",
						fontWeight: 600
					}}>
						{assignedCount}/{totalRoles}
					</span>
					<span style={{
						fontSize: 11,
						color: isFull ? "var(--danger)" : "var(--accent)",
						fontWeight: 600
					}}>
						{isFull ? "укомплектовано" : "есть места"}
					</span>
					<span className="pill">{badge}</span>
				</div>
			</div>
		</button>
	);
};

export default SongList;
