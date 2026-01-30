import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Code, ConnectError } from "@connectrpc/connect";

import { getProfile, getTgLoginLink, logout, telegramWebAppAuth } from "../services/api";
import { setTokenPair } from "../services/config";
import SongList from "./SongList";
import EventList from "./EventList";
import type { PermissionSet } from "../proto/permissions_pb";
import type { User } from "../proto/user_pb";
import type { ProfileResponse } from "../proto/auth_pb";

const AuthGate = () => {
	const [authError, setAuthError] = useState<string | null>(null);
	const [isAuthenticating, setIsAuthenticating] = useState(false);
	const [tgLinkError, setTgLinkError] = useState<string | null>(null);
	const [profileData, setProfileData] = useState<ProfileResponse | null>(null);
	const [profileError, setProfileError] = useState<Error | null>(null);
	const [isProfileLoading, setIsProfileLoading] = useState(true);
	const [isProfileOpen, setProfileOpen] = useState(false);
	const [isGettingTgLink, setIsGettingTgLink] = useState(false);
	const [needsTgLink, setNeedsTgLink] = useState(false);
	const hasAutoAuthAttempted = useRef(false);

	const loadProfile = useCallback(async () => {
		setIsProfileLoading(true);
		setProfileError(null);
		try {
			const data = await getProfile();
			setProfileData(data);
		} catch (err) {
			setProfileError(err as Error);
			setProfileData(null);
		} finally {
			setIsProfileLoading(false);
		}
	}, []);

	useEffect(() => {
		loadProfile();
	}, [loadProfile]);

	const isUnauthedCode = profileError instanceof ConnectError && profileError.code === Code.Unauthenticated;
	const profile = profileData?.profile as User | undefined;
	const permissions = profileData?.permissions as PermissionSet | undefined;

	useEffect(() => {
		const tg = window.Telegram?.WebApp;

		if (!tg || !tg.initData) {
			return;
		}

		// Signal to Telegram that the app is ready
		tg.ready();
		tg.expand();
	}, []);

	const requestTgLink = useCallback(
		async (userId?: string) => {
			if (isGettingTgLink) {
				return;
			}
			setTgLinkError(null);
			setIsGettingTgLink(true);
			try {
				const res = await getTgLoginLink(userId ? { id: userId } : undefined);
				if (res.loginLink) {
					window.open(res.loginLink, "_blank", "noopener");
				}
			} catch (err: any) {
				if (err instanceof ConnectError) {
					setTgLinkError(err.message);
				} else {
					setTgLinkError((err as Error).message);
				}
			} finally {
				setIsGettingTgLink(false);
			}
		},
		[isGettingTgLink],
	);

	const performTelegramAuth = useCallback(async () => {
		if (isAuthenticating || profileData) {
			return;
		}

		const tg = window.Telegram?.WebApp;
		if (!tg || !tg.initData) {
			setAuthError("Откройте приложение через Telegram Mini App");
			return;
		}

		setIsAuthenticating(true);
		setAuthError(null);
		setNeedsTgLink(false);

		try {
			const session = await telegramWebAppAuth(tg.initData);

			if (session.tokens?.accessToken == null || session.tokens?.refreshToken == null) {
				setAuthError("Сервер не вернул токены авторизации");
				return;
			}

			setTokenPair(session.tokens.accessToken, session.tokens.refreshToken);
			await loadProfile();
		} catch (err: any) {
			const message = err instanceof ConnectError ? err.message : (err as Error).message;
			setAuthError(message);
			if (/you must be a member/i.test(message)) {
				setNeedsTgLink(true);
				await requestTgLink(profile?.id);
			}
		} finally {
			setIsAuthenticating(false);
		}
	}, [isAuthenticating, profileData, loadProfile, requestTgLink, profile?.id]);

	useEffect(() => {
		const tg = window.Telegram?.WebApp;
		if (!isUnauthedCode || !tg?.initData) {
			return;
		}
		if (hasAutoAuthAttempted.current) {
			return;
		}
		hasAutoAuthAttempted.current = true;
		void performTelegramAuth();
	}, [isUnauthedCode, performTelegramAuth]);

	if (isProfileLoading) {
		return (
			<div className="card" style={{ maxWidth: 400, margin: "80px auto" }}>
				<div className="card-title">Загружаем профиль…</div>
				<div style={{ textAlign: "center", padding: "40px 0" }}>
					<div className="spinner" />
				</div>
			</div>
		);
	}

	if (isUnauthedCode) {
		// Check if we're in Telegram WebApp
		const tg = window.Telegram?.WebApp;

		if (!tg || !tg.initData) {
			// Not in Telegram - show error message
			return (
				<div className="card" style={{ maxWidth: 400, margin: "80px auto" }}>
					<div className="card-title" style={{ marginBottom: 16 }}>
						<span role="img" aria-label="music">
							🎸
						</span>
						Музыкальный клуб
					</div>
					<p style={{ color: "var(--muted)", lineHeight: 1.4, marginBottom: 24 }}>
						Это приложение доступно только через Telegram Mini App.
					</p>
					<div style={{
						padding: "16px",
						backgroundColor: "var(--accent-bg)",
						border: "1px solid var(--accent)",
						borderRadius: "8px",
						color: "var(--text)"
					}}>
						<strong style={{ display: "block", marginBottom: 8 }}>Как открыть приложение:</strong>
						<ol style={{ margin: 0, paddingLeft: 20, lineHeight: 1.6 }}>
							<li>Откройте Telegram</li>
							<li>Найдите бота <a href="https://t.me/cumusicclubbot">@cumusicclubbot</a></li>
							<li>Нажмите кнопку "Открыть приложение"</li>
						</ol>
					</div>
				</div>
			);
		}

		// In Telegram, show authenticating state
		return (
			<div className="card" style={{ maxWidth: 400, margin: "80px auto" }}>
				<div className="card-title" style={{ marginBottom: 16 }}>
					<span role="img" aria-label="music">
						🎸
					</span>
					Музыкальный клуб
				</div>
				{authError && (
					<div style={{
						padding: "16px",
						backgroundColor: "var(--danger-bg)",
						border: "1px solid var(--danger)",
						borderRadius: "8px",
						color: "var(--danger)",
						marginBottom: 16
					}}>
						{authError}
					</div>
				)}
				<div style={{ textAlign: "center", padding: "28px 0" }}>
					{isAuthenticating ? (
						<>
							<div className="spinner" style={{ marginBottom: 16 }} />
							<p style={{ color: "var(--muted)" }}>
								Авторизация через Telegram...
							</p>
						</>
					) : (
						<>
							{hasAutoAuthAttempted.current ? (
								<p style={{ color: "var(--muted)", marginBottom: 16 }}>
									Нажмите кнопку, чтобы подключиться через Telegram.
								</p>
							) : (
								<>
									<div className="spinner" style={{ marginBottom: 16 }} />
									<p style={{ color: "var(--muted)" }}>
										Пробуем подключиться автоматически...
									</p>
								</>
							)}
						</>
					)}
					{hasAutoAuthAttempted.current && !isAuthenticating && (
						<button
							className="button"
							type="button"
							disabled={isAuthenticating}
							onClick={() => performTelegramAuth()}
						>
							{isAuthenticating ? "Подключаем..." : "Подключиться"}
						</button>
					)}
					{needsTgLink && (
						<div style={{ marginTop: 12 }}>
							<button
								className="button secondary"
								type="button"
								disabled={isGettingTgLink}
								onClick={() => requestTgLink(profile?.id)}
							>
								{isGettingTgLink ? "Получаем..." : "Получить ссылку"}
							</button>
							{tgLinkError && (
								<div style={{ color: "var(--danger)", marginTop: 8 }}>{tgLinkError}</div>
							)}
						</div>
					)}
				</div>
			</div>
		);
	}

	if (profileError) {
		return (
			<div className="card" style={{ maxWidth: 400, margin: "80px auto" }}>
				<div className="card-title">Ошибка</div>
				<div style={{ padding: "20px", textAlign: "center" }}>
					<p style={{ color: "var(--danger)", marginBottom: 16 }}>
						Ошибка загрузки профиля: {profileError.message}
					</p>
					<button
						className="button"
						onClick={() => loadProfile()}
					>
						Попробовать снова
					</button>
				</div>
			</div>
		);
	}

	const hero = (
		<div className="card" style={{ marginBottom: 18 }}>
			<div className="section-header">
				<div className="card-title">
					<span role="img" aria-label="music">
						🎸
					</span>
					Музыкальный клуб
				</div>
				<button
					type="button"
					className="pill"
					style={{ cursor: "pointer" }}
					onClick={() => setProfileOpen(true)}
				>
					{profile?.avatarUrl ? (
						<img
							src={profile.avatarUrl}
							alt={profile.displayName}
							className="avatar-small"
						/>
					) : (
						<div
							className="status-dot"
							style={{ background: profile ? "var(--accent)" : "var(--muted)" }}
						/>
					)}
					{profile?.displayName}
				</button>
			</div>
			{profile && !profile.telegramId && (
				<div className="pill" style={{ justifyContent: "space-between", alignItems: "center", gap: 12 }}>
					<div style={{ flex: 1, minWidth: 0 }}>
						<div style={{ fontWeight: 600, marginBottom: 4 }}>Привяжите Telegram</div>
						<small style={{ color: "var(--muted)" }}>Получите ссылку для авторизации в боте</small>
						{tgLinkError && (
							<div style={{ color: "var(--danger)", marginTop: 8 }}>{tgLinkError}</div>
						)}
					</div>
					<button
						className="button"
						type="button"
						disabled={isGettingTgLink}
						onClick={() => requestTgLink(profile?.id)}
					>
						{isGettingTgLink ? "Получаем..." : "Получить ссылку"}
					</button>
				</div>
			)}
		</div>
	);

	return (
		<div className="grid">
			{hero}
			<SongList permissions={permissions} profile={profile} />
			<EventList permissions={permissions} />
			{isProfileOpen && profile && (
				<ProfileModal profile={profile} onClose={() => setProfileOpen(false)} />
			)}
		</div>
	);
};

const ProfileModal = ({ profile, onClose }: { profile: User; onClose: () => void }) => {
	return createPortal(
		<div className="modal-backdrop" onClick={onClose}>
			<div className="card modal-window" onClick={(e) => e.stopPropagation()}>
				<div className="section-header">
					<div className="card-title">
						<span role="img" aria-label="user">
							👤
						</span>
						{profile.displayName}
					</div>
					<button className="button secondary" onClick={onClose}>
						Закрыть
					</button>
				</div>
				<div style={{ color: "var(--muted)", marginBottom: 12 }}>
					Профиль пользователя
				</div>
				<div className="grid">
					<div className="pill" style={{ justifyContent: "space-between" }}>
						<span>Имя пользователя</span>
						<strong>{profile.username}</strong>
					</div>
					{profile.avatarUrl && (
						<div className="pill" style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" }}>
							<span>Аватар</span>
							<img
								src={profile.avatarUrl}
								alt={profile.displayName}
								className="avatar-small"
							/>
						</div>
					)}
				</div>
				<div style={{ marginTop: 18, display: "flex", gap: 10, justifyContent: "flex-end" }}>
					<button className="button danger" onClick={() => logout()}>
						Выйти
					</button>
				</div>
			</div>
		</div>,
		document.body,
	);
};

export default AuthGate;
