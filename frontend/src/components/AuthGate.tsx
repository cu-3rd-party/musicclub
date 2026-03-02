import { useCallback, useEffect, useRef, useState } from "react";
import { Code, ConnectError } from "@connectrpc/connect";

import { getProfile, getTgLoginLink, logout, telegramWebAppAuth } from "../services/api";
import { setTokenPair } from "../services/config";
import SongList from "./SongList";
import EventList from "./EventList";
import type { PermissionSet } from "../proto/permissions_pb";
import type { User } from "../proto/user_pb";
import type { ProfileResponse } from "../proto/auth_pb";
import "../styles/components/auth-gate.css";

const AuthGate = () => {
	const [authError, setAuthError] = useState<string | null>(null);
	const [isAuthenticating, setIsAuthenticating] = useState(false);
	const [tgLinkError, setTgLinkError] = useState<string | null>(null);
	const [profileData, setProfileData] = useState<ProfileResponse | null>(null);
	const [profileError, setProfileError] = useState<Error | null>(null);
	const [isProfileLoading, setIsProfileLoading] = useState(true);
	const [isGettingTgLink, setIsGettingTgLink] = useState(false);
	const [needsTgLink, setNeedsTgLink] = useState(false);
	const hasAutoAuthAttempted = useRef(false);
	const [activeTab, setActiveTab] = useState<"songs" | "events" | "profile">("songs");

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
			<div className="card auth-gate__center-card">
				<div className="card-title">Загружаем профиль…</div>
				<div className="auth-gate__spinner-wrap">
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
				<div className="card auth-gate__center-card">
					<div className="card-title auth-gate__card-title">
						<span role="img" aria-label="music">
							🎸
						</span>
						Музыкальный клуб
					</div>
					<p className="auth-gate__muted auth-gate__muted--large">
						Это приложение доступно только через Telegram Mini App.
					</p>
					<div className="auth-gate__info-box">
						<strong className="auth-gate__info-title">Как открыть приложение:</strong>
						<ol className="auth-gate__info-list">
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
			<div className="card auth-gate__center-card">
				<div className="card-title auth-gate__card-title">
					<span role="img" aria-label="music">
						🎸
					</span>
					Музыкальный клуб
				</div>
				{authError && (
					<div className="auth-gate__error-box">
						{authError}
					</div>
				)}
				<div className="auth-gate__auth-panel">
					{isAuthenticating ? (
						<>
							<div className="spinner auth-gate__spinner" />
							<p className="auth-gate__muted">
								Авторизация через Telegram...
							</p>
						</>
					) : (
						<>
							{hasAutoAuthAttempted.current ? (
								<p className="auth-gate__muted auth-gate__muted--spaced">
									Нажмите кнопку, чтобы подключиться через Telegram.
								</p>
							) : (
								<>
									<div className="spinner auth-gate__spinner" />
									<p className="auth-gate__muted">
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
						<div className="auth-gate__tg-actions">
							<button
								className="button secondary"
								type="button"
								disabled={isGettingTgLink}
								onClick={() => requestTgLink(profile?.id)}
							>
								{isGettingTgLink ? "Получаем..." : "Получить ссылку"}
							</button>
							{tgLinkError && (
								<div className="auth-gate__error-text">{tgLinkError}</div>
							)}
						</div>
					)}
				</div>
			</div>
		);
	}

	if (profileError) {
		return (
			<div className="card auth-gate__center-card">
				<div className="card-title">Ошибка</div>
				<div className="auth-gate__error-content">
					<p className="auth-gate__error-message">
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

	return (
		<div className="tabbed-layout">
			<div className="tab-content">
				{activeTab === "songs" && (
					<SongList permissions={permissions} profile={profile} />
				)}
				{activeTab === "events" && (
					<div className="grid">
						<div className="card hero-card">
							<div className="section-header">
								<div className="card-title">
									<span role="img" aria-label="calendar">
										📅
									</span>
									Афиша клуба
								</div>
								<div className="pill">Выступления и репетиции</div>
							</div>
							<div className="subtle">Следите за расписанием и собирайтесь на репетиции.</div>
						</div>
						<EventList permissions={permissions} />
					</div>
				)}
				{activeTab === "profile" && (
					<div className="grid">
						<div className="card profile-card">
							<div className="section-header">
								<div className="card-title">
									<span role="img" aria-label="user">
										👤
									</span>
									Профиль
								</div>
								<button className="button secondary" type="button" onClick={() => loadProfile()}>
									Обновить
								</button>
							</div>
							<div className="profile-summary">
								<div className="profile-avatar">
									{profile?.avatarUrl ? (
										<img src={profile.avatarUrl} alt={profile.displayName} />
									) : (
										<span>MC</span>
									)}
								</div>
								<div>
									<div className="profile-name">{profile?.displayName ?? "Гость"}</div>
									<div className="subtle">@{profile?.username || "musicclub"}</div>
								</div>
							</div>
							<div className="grid">
								<div className="pill auth-gate__pill">
									<span>Статус</span>
									<strong>{profile?.telegramId ? "Telegram привязан" : "Нужна привязка Telegram"}</strong>
								</div>
								<div className="pill auth-gate__pill">
									<span>ID</span>
									<strong>{profile?.id ?? "—"}</strong>
								</div>
							</div>
						</div>

						{profile && !profile.telegramId && (
							<div className="card">
								<div className="section-header">
									<div className="card-title">Привяжите Telegram</div>
									<button
										className="button"
										type="button"
										disabled={isGettingTgLink}
										onClick={() => requestTgLink(profile?.id)}
									>
										{isGettingTgLink ? "Получаем..." : "Получить ссылку"}
									</button>
								</div>
								<div className="subtle">Получите ссылку и пройдите авторизацию в боте.</div>
								{tgLinkError && <div className="auth-gate__error-text">{tgLinkError}</div>}
							</div>
						)}

						<div className="card">
							<div className="section-header">
								<div className="card-title">Аккаунт</div>
								<button className="button danger" type="button" onClick={() => logout()}>
									Выйти
								</button>
							</div>
							<div className="subtle">Вы можете выйти и заново авторизоваться через Telegram.</div>
						</div>
					</div>
				)}
			</div>
			<nav className="tab-bar" role="tablist">
				<button
					className={`tab-button ${activeTab === "songs" ? "active" : ""}`}
					type="button"
					role="tab"
					aria-selected={activeTab === "songs"}
					onClick={() => setActiveTab("songs")}
				>
					<span className="tab-icon" aria-hidden="true">🎵</span>
					<span className="tab-label">Песни</span>
				</button>
				<button
					className={`tab-button ${activeTab === "events" ? "active" : ""}`}
					type="button"
					role="tab"
					aria-selected={activeTab === "events"}
					onClick={() => setActiveTab("events")}
				>
					<span className="tab-icon" aria-hidden="true">📆</span>
					<span className="tab-label">События</span>
				</button>
				<button
					className={`tab-button ${activeTab === "profile" ? "active" : ""}`}
					type="button"
					role="tab"
					aria-selected={activeTab === "profile"}
					onClick={() => setActiveTab("profile")}
				>
					<span className="tab-icon" aria-hidden="true">👤</span>
					<span className="tab-label">Профиль</span>
				</button>
			</nav>
		</div>
	);
};

export default AuthGate;
