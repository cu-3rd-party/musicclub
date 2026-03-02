<template>
  <div v-if="isProfileLoading" class="card auth-gate__center-card">
    <div class="card-title">Загружаем профиль…</div>
    <div class="auth-gate__spinner-wrap">
      <div class="spinner" />
    </div>
  </div>

  <div v-else-if="isUnauthedCode">
    <div v-if="!hasTelegramApp" class="card auth-gate__center-card">
      <div class="card-title auth-gate__card-title">
        <span role="img" aria-label="music">🎸</span>
        Музыкальный клуб
      </div>
      <p class="auth-gate__muted auth-gate__muted--large">
        Это приложение доступно только через Telegram Mini App.
      </p>
      <div class="auth-gate__info-box">
        <strong class="auth-gate__info-title">Как открыть приложение:</strong>
        <ol class="auth-gate__info-list">
          <li>Откройте Telegram</li>
          <li>
            Найдите бота
            <a href="https://t.me/cumusicclubbot">@cumusicclubbot</a>
          </li>
          <li>Нажмите кнопку "Открыть приложение"</li>
        </ol>
      </div>
    </div>

    <div v-else class="card auth-gate__center-card">
      <div class="card-title auth-gate__card-title">
        <span role="img" aria-label="music">🎸</span>
        Музыкальный клуб
      </div>
      <div v-if="authError" class="auth-gate__error-box">{{ authError }}</div>
      <div class="auth-gate__auth-panel">
        <template v-if="isAuthenticating">
          <div class="spinner auth-gate__spinner" />
          <p class="auth-gate__muted">Авторизация через Telegram...</p>
        </template>
        <template v-else>
          <p
            v-if="hasAutoAuthAttempted"
            class="auth-gate__muted auth-gate__muted--spaced"
          >
            Нажмите кнопку, чтобы подключиться через Telegram.
          </p>
          <template v-else>
            <div class="spinner auth-gate__spinner" />
            <p class="auth-gate__muted">
              Пробуем подключиться автоматически...
            </p>
          </template>
        </template>
        <button
          v-if="hasAutoAuthAttempted && !isAuthenticating"
          class="button"
          type="button"
          :disabled="isAuthenticating"
          @click="performTelegramAuth"
        >
          {{ isAuthenticating ? "Подключаем..." : "Подключиться" }}
        </button>
        <div v-if="needsTgLink" class="auth-gate__tg-actions">
          <button
            class="button secondary"
            type="button"
            :disabled="isGettingTgLink"
            @click="requestTgLink(profile?.id)"
          >
            {{ isGettingTgLink ? "Получаем..." : "Получить ссылку" }}
          </button>
          <div v-if="tgLinkError" class="auth-gate__error-text">
            {{ tgLinkError }}
          </div>
        </div>
      </div>
    </div>
  </div>

  <div v-else-if="profileError" class="card auth-gate__center-card">
    <div class="card-title">Ошибка</div>
    <div class="auth-gate__error-content">
      <p class="auth-gate__error-message">
        Ошибка загрузки профиля: {{ profileError.message }}
      </p>
      <button class="button" @click="loadProfile">Попробовать снова</button>
    </div>
  </div>

  <div v-else class="tabbed-layout">
    <div class="tab-content">
      <SongList
        v-if="activeTab === 'songs'"
        :permissions="permissions"
        :profile="profile"
      />
      <div v-else-if="activeTab === 'events'" class="grid">
        <div class="card hero-card">
          <div class="section-header">
            <div class="card-title">
              <span role="img" aria-label="calendar">📅</span>
              Афиша клуба
            </div>
            <div class="pill">Выступления и репетиции</div>
          </div>
          <div class="subtle">
            Следите за расписанием и собирайтесь на репетиции.
          </div>
        </div>
        <EventList :permissions="permissions" />
      </div>
      <div v-else class="grid">
        <div class="card profile-card">
          <div class="section-header">
            <div class="card-title">
              <span role="img" aria-label="user">👤</span>
              Профиль
            </div>
            <button class="button secondary" type="button" @click="loadProfile">
              Обновить
            </button>
          </div>
          <div class="profile-summary">
            <div class="profile-avatar">
              <img
                v-if="profile?.avatarUrl"
                :src="profile.avatarUrl"
                :alt="profile.displayName"
              />
              <span v-else>MC</span>
            </div>
            <div>
              <div class="profile-name">
                {{ profile?.displayName ?? "Гость" }}
              </div>
              <div class="subtle">@{{ profile?.username || "musicclub" }}</div>
            </div>
          </div>
          <div class="grid">
            <div class="pill auth-gate__pill">
              <span>Статус</span>
              <strong>{{
                profile?.telegramId
                  ? "Telegram привязан"
                  : "Нужна привязка Telegram"
              }}</strong>
            </div>
            <div class="pill auth-gate__pill">
              <span>ID</span>
              <strong>{{ profile?.id ?? "—" }}</strong>
            </div>
          </div>
        </div>

        <div v-if="profile && !profile.telegramId" class="card">
          <div class="section-header">
            <div class="card-title">Привяжите Telegram</div>
            <button
              class="button"
              type="button"
              :disabled="isGettingTgLink"
              @click="requestTgLink(profile?.id)"
            >
              {{ isGettingTgLink ? "Получаем..." : "Получить ссылку" }}
            </button>
          </div>
          <div class="subtle">
            Получите ссылку и пройдите авторизацию в боте.
          </div>
          <div v-if="tgLinkError" class="auth-gate__error-text">
            {{ tgLinkError }}
          </div>
        </div>

        <div class="card">
          <div class="section-header">
            <div class="card-title">Аккаунт</div>
            <button class="button danger" type="button" @click="logout">
              Выйти
            </button>
          </div>
          <div class="subtle">
            Вы можете выйти и заново авторизоваться через Telegram.
          </div>
        </div>
      </div>
    </div>

    <nav class="tab-bar" role="tablist">
      <button
        class="tab-button"
        :class="{ active: activeTab === 'songs' }"
        type="button"
        role="tab"
        :aria-selected="activeTab === 'songs'"
        @click="activeTab = 'songs'"
      >
        <span class="tab-icon" aria-hidden="true">🎵</span>
        <span class="tab-label">Песни</span>
      </button>
      <button
        class="tab-button"
        :class="{ active: activeTab === 'events' }"
        type="button"
        role="tab"
        :aria-selected="activeTab === 'events'"
        @click="activeTab = 'events'"
      >
        <span class="tab-icon" aria-hidden="true">📆</span>
        <span class="tab-label">События</span>
      </button>
      <button
        class="tab-button"
        :class="{ active: activeTab === 'profile' }"
        type="button"
        role="tab"
        :aria-selected="activeTab === 'profile'"
        @click="activeTab = 'profile'"
      >
        <span class="tab-icon" aria-hidden="true">👤</span>
        <span class="tab-label">Профиль</span>
      </button>
    </nav>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { Code, ConnectError } from "@connectrpc/connect";

import {
  getProfile,
  getTgLoginLink,
  logout,
  telegramWebAppAuth,
} from "../services/api";
import { setTokenPair } from "../services/config";
import SongList from "./SongList.vue";
import EventList from "./EventList.vue";
import "../styles/domains/auth.css";

const authError = ref(null);
const isAuthenticating = ref(false);
const tgLinkError = ref(null);
const profileData = ref(null);
const profileError = ref(null);
const isProfileLoading = ref(true);
const isGettingTgLink = ref(false);
const needsTgLink = ref(false);
const hasAutoAuthAttempted = ref(false);
const activeTab = ref("songs");

const loadProfile = async () => {
  isProfileLoading.value = true;
  profileError.value = null;
  try {
    const data = await getProfile();
    profileData.value = data;
  } catch (err) {
    profileError.value = err;
    profileData.value = null;
  } finally {
    isProfileLoading.value = false;
  }
};

onMounted(() => {
  loadProfile();
});

const isUnauthedCode = computed(
  () =>
    profileError.value instanceof ConnectError &&
    profileError.value.code === Code.Unauthenticated,
);
const profile = computed(() => profileData.value?.profile);
const permissions = computed(() => profileData.value?.permissions);
const hasTelegramApp = computed(() =>
  Boolean(window.Telegram?.WebApp?.initData),
);

onMounted(() => {
  const tg = window.Telegram?.WebApp;
  if (!tg || !tg.initData) {
    return;
  }
  tg.ready();
  tg.expand();
});

const requestTgLink = async (userId) => {
  if (isGettingTgLink.value) return;
  tgLinkError.value = null;
  isGettingTgLink.value = true;
  try {
    const res = await getTgLoginLink(userId ? { id: userId } : undefined);
    if (res.loginLink) {
      window.open(res.loginLink, "_blank", "noopener");
    }
  } catch (err) {
    tgLinkError.value = err instanceof ConnectError ? err.message : err.message;
  } finally {
    isGettingTgLink.value = false;
  }
};

const performTelegramAuth = async () => {
  if (isAuthenticating.value || profileData.value) return;

  const tg = window.Telegram?.WebApp;
  if (!tg || !tg.initData) {
    authError.value = "Откройте приложение через Telegram Mini App";
    return;
  }

  isAuthenticating.value = true;
  authError.value = null;
  needsTgLink.value = false;

  try {
    const session = await telegramWebAppAuth(tg.initData);
    if (
      session.tokens?.accessToken == null ||
      session.tokens?.refreshToken == null
    ) {
      authError.value = "Сервер не вернул токены авторизации";
      return;
    }
    setTokenPair(session.tokens.accessToken, session.tokens.refreshToken);
    await loadProfile();
  } catch (err) {
    const message = err instanceof ConnectError ? err.message : err.message;
    authError.value = message;
    if (/you must be a member/i.test(message)) {
      needsTgLink.value = true;
      await requestTgLink(profile.value?.id);
    }
  } finally {
    isAuthenticating.value = false;
  }
};

watch(isUnauthedCode, (value) => {
  const tg = window.Telegram?.WebApp;
  if (!value || !tg?.initData) {
    return;
  }
  if (hasAutoAuthAttempted.value) {
    return;
  }
  hasAutoAuthAttempted.value = true;
  void performTelegramAuth();
});
</script>
