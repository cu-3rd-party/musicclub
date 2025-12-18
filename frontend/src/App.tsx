import { JSXElementConstructor, ReactElement, ReactNode, ReactPortal, useMemo, useState } from "react";
import {
  useConcerts,
  useCreateConcert,
  useCreateParticipation,
  useCreateSong,
  useDeleteConcert,
  useDeleteParticipation,
  useDeleteSong,
  useLogin,
  useParticipations,
  useSongs,
  useLogout
} from "./grpc/hooks";
import { Timestamp } from "@bufbuild/protobuf";
import { getToken } from "./lib/auth";

type ConcertFormState = { name: string; date: string };
type SongFormState = { title: string; description: string; link: string };
type ParticipationFormState = { tg_id: string; song_id: string; role_title: string };

export default function App() {
  const [tgId, setTgId] = useState("");
  const [loginError, setLoginError] = useState("");
  const login = useLogin();
  const logout = useLogout();
  const token = getToken();
  const [isAdmin, setIsAdmin] = useState(false);

  const [songForm, setSongForm] = useState<SongFormState>({
    title: "",
    description: "",
    link: ""
  });

  const [concertForm, setConcertForm] = useState<ConcertFormState>({
    name: "",
    date: ""
  });

  const [participationForm, setParticipationForm] =
    useState<ParticipationFormState>({
      tg_id: "",
      song_id: "",
      role_title: ""
    });

  const { data: songs, isLoading: songsLoading } = useSongs();
  const { data: concerts, isLoading: concertsLoading } = useConcerts();
  const {
    data: participations,
    isLoading: participationsLoading
  } = useParticipations();

  const createSong = useCreateSong();
  const createConcert = useCreateConcert();
  const createParticipation = useCreateParticipation();
  const deleteSong = useDeleteSong();
  const deleteConcert = useDeleteConcert();
  const deleteParticipation = useDeleteParticipation();

  const heroStats = useMemo(() => {
    return [
      { label: "Songs", value: songs?.length ?? 0 },
      { label: "Concerts", value: concerts?.length ?? 0 },
      { label: "Roles", value: participations?.length ?? 0 }
    ];
  }, [songs, concerts, participations]);

  const handleLogin = async () => {
    setLoginError("");
    try {
      const result = await login.mutateAsync(tgId);
      setIsAdmin(result.isAdmin);
    } catch (err) {
      setLoginError("Login failed. Check Telegram ID and backend availability.");
    }
  };

  const resetForms = () => {
    setSongForm({ title: "", description: "", link: "" });
    setConcertForm({ name: "", date: "" });
    setParticipationForm({ tg_id: "", song_id: "", role_title: "" });
  };

  const submitSong = async () => {
    if (!songForm.title.trim()) return;
    await createSong.mutateAsync({
      id: BigInt(0),
      title: songForm.title,
      description: songForm.description,
      link: songForm.link
    });
    resetForms();
  };

  const submitConcert = async () => {
    if (!concertForm.name.trim() || !concertForm.date) return;
    const ts = Timestamp.fromDate(new Date(concertForm.date));
    await createConcert.mutateAsync({
      id: BigInt(0),
      name: concertForm.name,
      date: ts
    });
    resetForms();
  };

  const submitParticipation = async () => {
    if (!participationForm.tg_id || !participationForm.song_id) return;
    await createParticipation.mutateAsync({
      tg_id: BigInt(participationForm.tg_id),
      song_id: BigInt(participationForm.song_id),
      role_title: participationForm.role_title
    });
    resetForms();
  };

  return (
    <div className="app-shell">
      <header className="hero">
        <div className="card">
          <div className="section-header">
            <div>
              <div className="pill">
                <span className="status-dot" /> Live club board
              </div>
              <h1 style={{ margin: "10px 0 8px" }}>CU Music Club</h1>
              {/* <p className="subtle">
                Manage songs, concerts, and member slots. Optimized for Telegram
                Mini App embedding.
              </p> */}
            </div>
            {/* <div className="tags">
              <span className="pill">gRPC</span>
              <span className="pill">Realtime feel</span>
              <span className="pill">60fps UI</span>
            </div> */}
          </div>
          <div style={{ display: "flex", gap: 10, marginTop: 12 }}>
            {!token ? (
              <>
                <input
                  className="input"
                  placeholder="Enter Telegram ID to login"
                  value={tgId}
                  onChange={(e) => setTgId(e.target.value)}
                  inputMode="numeric"
                />
                <button className="button" onClick={handleLogin}>
                  Login
                </button>
              </>
            ) : (
              <>
                <div className="pill">
                  Logged in · {isAdmin ? "Admin" : "Member"}
                </div>
                <button
                  className="button secondary"
                  onClick={() => {
                    setIsAdmin(false);
                    logout();
                  }}
                >
                  Logout
                </button>
              </>
            )}
          </div>
          {loginError && (
            <p style={{ color: "var(--danger)", marginTop: 8 }}>{loginError}</p>
          )}
        </div>
        <div className="hero-visual">
          <div className="pulse" />
          <div className="pulse secondary" />
        </div>
      </header>

      <section className="grid" style={{ marginBottom: 18 }}>
        <div className="card">
          <div className="section-header">
            <div className="card-title">Song library</div>
            <div className="tags">
              {heroStats.map((stat) => (
                <span key={stat.label} className="pill">
                  {stat.label}: {stat.value}
                </span>
              ))}
            </div>
          </div>

          {isAdmin && (
            <div className="two-col" style={{ marginBottom: 10 }}>
              <input
                className="input"
                placeholder="Title"
                value={songForm.title}
                onChange={(e) =>
                  setSongForm((prev) => ({ ...prev, title: e.target.value }))
                }
              />
              <input
                className="input"
                placeholder="External link"
                value={songForm.link}
                onChange={(e) =>
                  setSongForm((prev) => ({ ...prev, link: e.target.value }))
                }
              />
              <textarea
                className="textarea"
                placeholder="Description"
                value={songForm.description}
                onChange={(e) =>
                  setSongForm((prev) => ({
                    ...prev,
                    description: e.target.value
                  }))
                }
                rows={3}
              />
              <button className="button" onClick={submitSong}>
                Add song
              </button>
            </div>
          )}

          {songsLoading ? (
            <p className="subtle">Loading songs...</p>
          ) : (
            <div className="scroll-y">
              {songs?.map((song: { id: any; title: string | number | boolean | ReactElement<any, string | JSXElementConstructor<any>> | Iterable<ReactNode> | ReactPortal | null | undefined; description: string | number | boolean | ReactElement<any, string | JSXElementConstructor<any>> | Iterable<ReactNode> | ReactPortal | null | undefined; link: string | undefined; }) => (
                <div className="list-row" key={String(song.id)}>
                  <div>
                    <div className="card-title">{song.title}</div>
                    <p className="subtle">{song.description}</p>
                  </div>
                  <a
                    href={song.link}
                    target="_blank"
                    rel="noreferrer"
                    className="pill"
                  >
                    Listen / details
                  </a>
                  {isAdmin && (
                    <button
                      className="button danger"
                      onClick={() => deleteSong.mutate(String(song.id))}
                    >
                      Delete
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="two-col" style={{ marginBottom: 18 }}>
        <div className="card">
          <div className="section-header">
            <div className="card-title">Concerts</div>
            <div className="pill">Schedule</div>
          </div>

          {isAdmin && (
            <div className="two-col" style={{ marginBottom: 10 }}>
              <input
                className="input"
                placeholder="Concert name"
                value={concertForm.name}
                onChange={(e) =>
                  setConcertForm((prev) => ({ ...prev, name: e.target.value }))
                }
              />
              <input
                className="input"
                type="datetime-local"
                value={concertForm.date}
                onChange={(e) =>
                  setConcertForm((prev) => ({ ...prev, date: e.target.value }))
                }
              />
              <button className="button" onClick={submitConcert}>
                Add concert
              </button>
            </div>
          )}

          {concertsLoading ? (
            <p className="subtle">Loading concerts...</p>
          ) : (
            <div className="scroll-y">
              {concerts?.map((concert: { id: any; name: string | number | boolean | ReactElement<any, string | JSXElementConstructor<any>> | Iterable<ReactNode> | ReactPortal | null | undefined; date: { toDate: () => { (): any; new(): any; toLocaleString: { (): any; new(): any; }; }; }; }) => (
                <div className="list-row" key={String(concert.id)}>
                  <div>
                    <div className="card-title">{concert.name}</div>
                    <p className="subtle">
                      {concert.date?.toDate().toLocaleString() ?? "TBD"}
                    </p>
                  </div>
                  <div className="pill">
                    <span className="status-dot" />
                    Upcoming
                  </div>
                  {isAdmin && (
                    <button
                      className="button danger"
                      onClick={() => deleteConcert.mutate(String(concert.id))}
                    >
                      Delete
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="card">
          <div className="section-header">
            <div className="card-title">Participations</div>
            <div className="pill">Roles</div>
          </div>

          {token && (
            <div className="two-col" style={{ marginBottom: 10 }}>
              <input
                className="input"
                placeholder="Telegram ID"
                value={participationForm.tg_id}
                onChange={(e) =>
                  setParticipationForm((prev) => ({
                    ...prev,
                    tg_id: e.target.value
                  }))
                }
              />
              <input
                className="input"
                placeholder="Song ID"
                value={participationForm.song_id}
                onChange={(e) =>
                  setParticipationForm((prev) => ({
                    ...prev,
                    song_id: e.target.value
                  }))
                }
              />
              <input
                className="input"
                placeholder="Role (e.g. Guitar, Vocal)"
                value={participationForm.role_title}
                onChange={(e) =>
                  setParticipationForm((prev) => ({
                    ...prev,
                    role_title: e.target.value
                  }))
                }
              />
              <button className="button" onClick={submitParticipation}>
                Add participation
              </button>
            </div>
          )}

          {participationsLoading ? (
            <p className="subtle">Loading participations...</p>
          ) : (
            <div className="scroll-y">
              {participations?.map((p: { tg_id: any; song_id: any; role_title: string | number | boolean | ReactElement<any, string | JSXElementConstructor<any>> | Iterable<ReactNode> | ReactPortal | null | undefined; }) => (
                <div className="list-row" key={`${p.tg_id}-${p.song_id}`}>
                  <div>
                    <div className="card-title">{p.role_title}</div>
                    <p className="subtle">TG: {String(p.tg_id)}</p>
                  </div>
                  <div className="pill">Song #{String(p.song_id)}</div>
                  {isAdmin && (
                    <button
                      className="button danger"
                      onClick={() =>
                        deleteParticipation.mutate(
                          `${p.tg_id}-${p.song_id}-${p.role_title}`
                        )
                      }
                    >
                      Remove
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}
