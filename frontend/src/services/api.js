import { createClient } from "@connectrpc/connect";
import { AuthService, TelegramWebAppAuthRequestSchema } from "../proto/auth_pb";
import { create } from "@bufbuild/protobuf";

import { clearTokenPair, transport } from "./config";
import { SongService } from "../proto/song_pb";
import { EventService } from "../proto/event_pb";
import { UserSchema } from "../proto/user_pb";

export const authClient = createClient(AuthService, transport);
export const songClient = createClient(SongService, transport);
export const eventClient = createClient(EventService, transport);

// Login with username/password
export const login = async (credentials) => {
  return await authClient.login(credentials);
};

// Register new user
export const register = async (request) => {
  return await authClient.register(request);
};

// Authenticate via Telegram WebApp
export const telegramWebAppAuth = async (initData) => {
  return await authClient.telegramWebAppAuth(
    create(TelegramWebAppAuthRequestSchema, { initData }),
  );
};

// Clear all login state
export const logout = () => {
  clearTokenPair();
  window.location.href = "/";
};

export function getTgLoginLink(user) {
  return authClient.getTgLoginLink(create(UserSchema, user ?? {}));
}

export function getProfile() {
  return authClient.getProfile({});
}

export function listSongs(query = "", pageToken = "", pageSize = 20) {
  return songClient.listSongs({ query, pageToken, pageSize });
}

export function getSong(id) {
  return songClient.getSong({ id });
}

export function createSong(payload) {
  return songClient.createSong({
    title: payload.title,
    artist: payload.artist,
    description: payload.description ?? "",
    link: { url: payload.linkUrl, kind: payload.linkKind },
    availableRoles: payload.roles,
    thumbnailUrl: payload.thumbnailUrl ?? "",
    featured: payload.featured ?? false,
  });
}

export function updateSong(payload) {
  return songClient.updateSong({
    id: payload.id,
    title: payload.title,
    artist: payload.artist,
    description: payload.description ?? "",
    link: { url: payload.linkUrl, kind: payload.linkKind },
    availableRoles: payload.roles,
    thumbnailUrl: payload.thumbnailUrl ?? "",
    featured: payload.featured ?? false,
  });
}

export function deleteSong(id) {
  return songClient.deleteSong({ id });
}

export function joinSongRole(songId, role) {
  return songClient.joinRole({ songId, role });
}

export function leaveSongRole(songId, role) {
  return songClient.leaveRole({ songId, role });
}

export function listEvents(from, to, limit = 50) {
  return eventClient.listEvents({ from, to, limit });
}

export function getEvent(id) {
  return eventClient.getEvent({ id });
}

export function createEvent(payload) {
  return eventClient.createEvent({
    title: payload.title,
    startAt: payload.startAt,
    location: payload.location ?? "",
    notifyDayBefore: payload.notifyDayBefore ?? false,
    notifyHourBefore: payload.notifyHourBefore ?? false,
    tracklist: payload.tracklist
      ? {
          items: payload.tracklist.map((i) => ({
            order: i.order,
            songId: i.songId,
            customTitle: i.customTitle,
            customArtist: i.customArtist,
          })),
        }
      : undefined,
  });
}

export function updateEvent(payload) {
  return eventClient.updateEvent({
    id: payload.id,
    title: payload.title,
    startAt: payload.startAt,
    location: payload.location ?? "",
    notifyDayBefore: payload.notifyDayBefore ?? false,
    notifyHourBefore: payload.notifyHourBefore ?? false,
  });
}

export function deleteEvent(id) {
  return eventClient.deleteEvent({ id });
}

export function setTracklist(eventId, items) {
  return eventClient.setTracklist({
    eventId,
    tracklist: {
      items: items.map((i) => ({
        order: i.order,
        songId: i.songId,
        customTitle: i.customTitle,
        customArtist: i.customArtist,
      })),
    },
  });
}
