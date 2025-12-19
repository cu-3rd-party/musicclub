import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createClient } from "@connectrpc/connect";
import { SongService, type Song } from "../gen/song_pb.js";
import { ConcertService, type Concert } from "../gen/concert_pb.js";
import { ParticipationService } from "../gen/participation_pb.js";
import { AuthService } from "../gen/auth_pb.js";
import { transport, setToken } from "../services/config.js";
import { makeTimestamp } from "./makeTimestamp.js";
import { safeId } from "./safeId.js";
import { LoginCard } from "./LoginCard.js";
import { StatusPills } from "./StatusPills.js";
import { SongsCard } from "./SongsCard.js";
import { ConcertsCard } from "./ConcertsCard.js";
import { JoinCard } from "./JoinCard.js";
import { MyParticipationsCard } from "./MyParticipationsCard.js";

declare global {
  interface Window {
    Telegram?: {
      WebApp?: {
        initDataUnsafe?: unknown;
      };
    };
  }
}

const CLUB_PARENT = "clubs/main";

function validateInitDataUnsafe(_initDataUnsafe: unknown): boolean {
  // TODO: add server-side validation of initData once available.
  return true;
}

function extractTelegramUserId(initDataUnsafe: any): string | null {
  const userId = initDataUnsafe?.user?.id ?? initDataUnsafe?.user?.ID;
  if (userId == null) return null;
  return String(userId);
}

export default function App() {
  const queryClient = useQueryClient();
  const songClient = useMemo(() => createClient(SongService, transport), []);
  const concertClient = useMemo(() => createClient(ConcertService, transport), []);
  const participationClient = useMemo(() => createClient(ParticipationService, transport), []);
  const authClient = useMemo(() => createClient(AuthService, transport), []);

  const [tgIdInput, setTgIdInput] = useState("");
  const [loginStatus, setLoginStatus] = useState<string | null>(null);
  const [isAdmin, setIsAdmin] = useState(false);
  const autoLoginAttempted = useRef(false);
  const [loginDebug, setLoginDebug] = useState<string[]>([]);

  const appendLoginDebug = useCallback((msg: string) => {
    setLoginDebug((prev) => [...prev, msg]);
  }, []);

  const songsQuery = useQuery({
    queryKey: ["songs"],
    queryFn: async () => {
      const res = await songClient.listSongs({ parent: CLUB_PARENT, pageSize: 100, pageToken: "" });
      return res.songs ?? [];
    },
  });

  const concertsQuery = useQuery({
    queryKey: ["concerts"],
    queryFn: async () => {
      const res = await concertClient.listConcerts({ parent: CLUB_PARENT, pageSize: 100, pageToken: "" });
      return res.concerts ?? [];
    },
  });

  const participationsQuery = useQuery({
    queryKey: ["participations"],
    queryFn: async () => {
      const res = await participationClient.listParticipations({
        parent: CLUB_PARENT,
        pageSize: 200,
        pageToken: "",
      });
      return res.participations ?? [];
    },
  });

  const loginMutation = useMutation({
    mutationFn: async (tg: string) => {
      appendLoginDebug(`loginTg requested for id ${tg}`);
      return authClient.loginTg({ tgId: BigInt(tg) });
    },
    onSuccess: (data, variables) => {
      setToken(data.token);
      setIsAdmin(data.isAdmin);
      setLoginStatus(`Token issued, admin: ${data.isAdmin ? "yes" : "no"}`);
      setTgIdInput(variables);
      appendLoginDebug(`Login success for ${variables}; admin=${data.isAdmin}`);
    },
    onError: (err: unknown) => {
      setLoginStatus(`Login failed: ${String(err)}`);
      appendLoginDebug(`Login failed: ${String(err)}`);
    },
  });

  const songMutation = useMutation({
    mutationFn: async (payload: { id?: bigint; title: string; description: string; link: string }) => {
      if (payload.id) {
        return songClient.updateSong({
          song: {
            id: payload.id,
            title: payload.title,
            description: payload.description,
            link: payload.link,
          },
          updateMask: { paths: ["title", "description", "link"] },
        });
      }
      return songClient.createSong({
        parent: CLUB_PARENT,
        songId: safeId(),
        song: {
          id: 0n,
          title: payload.title,
          description: payload.description,
          link: payload.link,
        },
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["songs"] });
    },
  });

  const deleteSongMutation = useMutation({
    mutationFn: async (song: Song) => {
      return songClient.deleteSong({ name: song.id.toString() });
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["songs"] }),
  });

  const concertMutation = useMutation({
    mutationFn: async (payload: { id?: bigint; name: string; date: string }) => {
      const ts = makeTimestamp(payload.date);
      if (payload.id) {
        const concertUpdate: any = { id: payload.id, name: payload.name };
        const paths: string[] = ["name"];
        if (ts) {
          concertUpdate.date = ts;
          paths.push("date");
        }
        return concertClient.updateConcert({
          concert: concertUpdate,
          updateMask: { paths },
        });
      }
      return concertClient.createConcert({
        parent: CLUB_PARENT,
        concertId: safeId(),
        concert: ts
          ? {
            id: 0n,
            name: payload.name,
            date: ts,
          }
          : {
            id: 0n,
            name: payload.name,
          },
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["concerts"] });
    },
  });

  const deleteConcertMutation = useMutation({
    mutationFn: async (concert: Concert) => {
      return concertClient.deleteConcert({ name: concert.id.toString() });
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["concerts"] }),
  });

  const participationMutation = useMutation({
    mutationFn: async (payload: { tgId: string; songId: string; roleTitle: string }) => {
      return participationClient.createParticipation({
        parent: CLUB_PARENT,
        participationId: safeId(),
        participation: {
          tgId: BigInt(payload.tgId),
          songId: BigInt(payload.songId),
          roleTitle: payload.roleTitle,
        },
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["participations"] });
    },
  });

  useEffect(() => {
    const webApp = window?.Telegram?.WebApp;
    appendLoginDebug(`Telegram WebApp detected: ${Boolean(webApp)}`);
    const initDataUnsafe = webApp?.initDataUnsafe;
    if (!initDataUnsafe || autoLoginAttempted.current) {
      appendLoginDebug("initDataUnsafe missing or auto-login already attempted");
      return;
    }
    if (!validateInitDataUnsafe(initDataUnsafe)) {
      appendLoginDebug("initDataUnsafe validation failed");
      return;
    }
    const extractedId = extractTelegramUserId(initDataUnsafe);
    if (!extractedId) {
      appendLoginDebug("No user id found in initDataUnsafe");
      return;
    }
    autoLoginAttempted.current = true;
    appendLoginDebug(`initDataUnsafe found; user id=${extractedId}`);
    setTgIdInput(extractedId);
    setLoginStatus("Logging in via Telegram...");
    loginMutation.mutate(extractedId);
  }, [appendLoginDebug, loginMutation]);

  const songMap = useMemo(() => {
    const map = new Map<string, Song>();
    const items = songsQuery.data ?? [];
    for (const song of items) {
      map.set(song.id.toString(), song);
    }
    return map;
  }, [songsQuery.data]);

  const myParticipations = useMemo(() => {
    if (!tgIdInput) {
      return [];
    }
    const list = participationsQuery.data ?? [];
    return list.filter((p) => p.tgId.toString() === tgIdInput);
  }, [participationsQuery.data, tgIdInput]);

  return (
    <div className="app-shell">
      {/* <Legend /> */}

      <div className="grid two-col">
        <LoginCard
          tgIdInput={tgIdInput}
          setTgIdInput={setTgIdInput}
          loginStatus={loginStatus}
          onLogin={(tg) => loginMutation.mutate(tg)}
          loading={loginMutation.isPending}
          debugLines={loginDebug}
        />
        <StatusPills
          songsCount={songsQuery.data?.length ?? 0}
          concertsCount={concertsQuery.data?.length ?? 0}
          participationsCount={participationsQuery.data?.length ?? 0}
          fetching={songsQuery.isFetching || concertsQuery.isFetching || participationsQuery.isFetching}
        />
      </div>

      <div className="grid two-col" style={{ marginTop: 18 }}>
        <SongsCard
          songs={songsQuery.data ?? []}
          isFetching={songsQuery.isFetching}
          onSave={(payload) => songMutation.mutate(payload)}
          onDelete={(song) => deleteSongMutation.mutate(song)}
          saving={songMutation.isPending}
          deleting={deleteSongMutation.isPending}
        />

        <ConcertsCard
          concerts={concertsQuery.data ?? []}
          isFetching={concertsQuery.isFetching}
          onSave={(payload) => concertMutation.mutate(payload)}
          onDelete={(concert) => deleteConcertMutation.mutate(concert)}
          saving={concertMutation.isPending}
          deleting={deleteConcertMutation.isPending}
          isAdmin={isAdmin}
        />
      </div>

      <div className="grid two-col" style={{ marginTop: 18 }}>
        <JoinCard
          songs={songsQuery.data ?? []}
          tgId={tgIdInput}
          onJoin={(payload) => participationMutation.mutate(payload)}
          saving={participationMutation.isPending}
        />
        <MyParticipationsCard tgId={tgIdInput} participations={myParticipations} songMap={songMap} />
      </div>
    </div>
  );
}
