<template>
  <div class="song-list-screen">
    <div class="song-list">
      <div class="song-list-body">
        <div v-if="listState.isLoading && listState.items.length === 0">
          Загружаем песни…
        </div>
        <div v-if="listState.error" class="song-list__error">
          Ошибка: {{ listState.error.message }}
        </div>

        <div v-if="filteredItems.length > 0" class="grid">
          <SongRow
            v-for="song in filteredItems"
            :key="song.id"
            :song="song"
            @open="selectedId = song.id"
          />
        </div>

        <div v-if="hasNextPage" class="song-list__load-more">
          <button
            class="button"
            :disabled="listState.isFetchingNext"
            @click="fetchSongs(false)"
          >
            {{ listState.isFetchingNext ? "Загружаем…" : "Показать еще" }}
          </button>
        </div>
        <div ref="loadMoreRef" class="song-list__load-sentinel" />
      </div>
    </div>

    <div class="song-search-bar">
      <div class="song-search-bar-inner">
        <input
          class="input"
          placeholder="Поиск по названию или исполнителю"
          v-model="query"
        />
        <div class="dropdown" ref="filterMenuRef">
          <button
            class="button secondary"
            type="button"
            aria-haspopup="true"
            :aria-expanded="isFilterOpen"
            @click="isFilterOpen = !isFilterOpen"
          >
            Фильтры
          </button>
          <div v-if="isFilterOpen" class="dropdown-menu">
            <label class="checkbox">
              <input
                class="checkbox-input"
                type="checkbox"
                v-model="showFull"
              />
              <span class="checkbox-box" aria-hidden="true" />
              <span class="checkbox-label">укомплектованные</span>
            </label>
            <label class="checkbox">
              <input
                class="checkbox-input"
                type="checkbox"
                v-model="showNotFull"
              />
              <span class="checkbox-box" aria-hidden="true" />
              <span class="checkbox-label">с местами</span>
            </label>
          </div>
        </div>
      </div>
    </div>

    <div v-if="canCreate" class="song-fab-wrap">
      <div v-if="isCreateOpen" class="card song-create-dialog">
        <div class="section-header">
          <div class="card-title">Новая песня</div>
          <button
            class="button secondary"
            type="button"
            @click="isCreateOpen = false"
          >
            Закрыть
          </button>
        </div>
        <CreateSongForm :can-feature="canFeature" :on-submit="handleCreate" />
      </div>
      <button
        class="song-fab"
        type="button"
        @click="isCreateOpen = !isCreateOpen"
      >
        {{ isCreateOpen ? "×" : "+" }}
      </button>
    </div>

    <SongModal
      v-if="selectedId && details && !isDetailLoading && !detailError"
      :details="details"
      :can-edit="canEdit"
      :can-edit-any="canEditAny"
      :current-user-id="profile?.id ?? ''"
      @close="selectedId = null"
      @join="handleJoin"
      @leave="handleLeave"
      @update="handleUpdate"
      @delete="handleDelete"
    />
  </div>
</template>

<script setup>
import {
  computed,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
  watchEffect,
} from "vue";
import {
  createSong,
  deleteSong,
  getSong,
  joinSongRole,
  leaveSongRole,
  listSongs,
  updateSong,
} from "../services/api";
import SongModal from "./SongModal.vue";
import CreateSongForm from "./forms/CreateSongForm.vue";
import SongRow from "./SongRow.vue";
import "../styles/domains/songs.css";

const props = defineProps({
  permissions: { type: Object, default: undefined },
  profile: { type: Object, default: undefined },
});

const query = ref("");
const selectedId = ref(null);
const showFull = ref(true);
const showNotFull = ref(true);
const isFilterOpen = ref(false);
const listState = reactive({
  items: [],
  nextPageToken: undefined,
  isLoading: false,
  isFetchingNext: false,
  error: null,
});
const nextPageTokenRef = ref(undefined);
const details = ref(null);
const isDetailLoading = ref(false);
const detailError = ref(null);
const wasHidden = ref(false);
const filterMenuRef = ref(null);
const loadMoreRef = ref(null);
const isCreateOpen = ref(false);

const fetchSongs = async (reset = false) => {
  listState.isLoading = reset ? true : listState.isLoading;
  listState.isFetchingNext = reset ? false : true;
  listState.error = null;

  const pageToken = reset ? "" : (nextPageTokenRef.value ?? "");
  if (reset) {
    nextPageTokenRef.value = undefined;
  }
  try {
    const res = await listSongs(query.value, pageToken);
    const incomingSongs = (res.songs ?? []).filter(Boolean);
    nextPageTokenRef.value = res.nextPageToken || undefined;
    listState.items = reset
      ? incomingSongs
      : [...listState.items, ...incomingSongs];
    listState.nextPageToken = nextPageTokenRef.value;
  } catch (err) {
    listState.error = err;
  } finally {
    listState.isLoading = false;
    listState.isFetchingNext = false;
  }
};

watch(
  query,
  () => {
    listState.isLoading = true;
    listState.error = null;
    listState.nextPageToken = undefined;
    fetchSongs(true);
  },
  { immediate: true },
);

const markHidden = () => {
  wasHidden.value = true;
};
const handleFocus = () => {
  if (!wasHidden.value) return;
  wasHidden.value = false;
  fetchSongs(true);
};
const handleVisibility = () => {
  if (document.hidden) {
    wasHidden.value = true;
    return;
  }
  handleFocus();
};

onMounted(() => {
  window.addEventListener("focus", handleFocus);
  window.addEventListener("blur", markHidden);
  document.addEventListener("visibilitychange", handleVisibility);
});

onBeforeUnmount(() => {
  window.removeEventListener("focus", handleFocus);
  window.removeEventListener("blur", markHidden);
  document.removeEventListener("visibilitychange", handleVisibility);
});

watch(isFilterOpen, (open, _, onCleanup) => {
  if (!open) return;
  const handleClickOutside = (event) => {
    if (!filterMenuRef.value) return;
    if (filterMenuRef.value.contains(event.target)) return;
    isFilterOpen.value = false;
  };
  document.addEventListener("mousedown", handleClickOutside);
  onCleanup(() =>
    document.removeEventListener("mousedown", handleClickOutside),
  );
});

const fetchDetails = async (songId) => {
  isDetailLoading.value = true;
  detailError.value = null;
  try {
    const res = await getSong(songId);
    details.value = res;
  } catch (err) {
    details.value = null;
    detailError.value = err;
  } finally {
    isDetailLoading.value = false;
  }
};

watch(selectedId, (value) => {
  if (!value) {
    details.value = null;
    detailError.value = null;
    return;
  }
  fetchDetails(value);
});

const canCreate = computed(() =>
  Boolean(
    props.permissions?.songs?.editAnySongs ||
    props.permissions?.songs?.editOwnSongs,
  ),
);
const canFeature = computed(() =>
  Boolean(props.permissions?.songs?.editFeaturedSongs),
);
const hasNextPage = computed(() => Boolean(listState.nextPageToken));
const filteredItems = computed(() => {
  if (showFull.value && showNotFull.value) {
    return listState.items;
  }
  return listState.items.filter((song) => {
    const totalRoles = song.availableRoles?.length || 0;
    const assignedCount = song.assignmentCount || 0;
    const isFull = assignedCount >= totalRoles;
    return (showFull.value && isFull) || (showNotFull.value && !isFull);
  });
});

const canEdit = computed(() =>
  Boolean(
    details.value?.permissions?.songs?.editAnySongs ||
    details.value?.permissions?.songs?.editOwnSongs,
  ),
);
const canEditAny = computed(() =>
  Boolean(details.value?.permissions?.songs?.editAnySongs),
);

watchEffect((onCleanup) => {
  if (!hasNextPage.value || listState.isFetchingNext || listState.isLoading)
    return;
  const node = loadMoreRef.value;
  if (!node) return;
  const observer = new IntersectionObserver(
    (entries) => {
      const [entry] = entries;
      if (
        entry?.isIntersecting &&
        !listState.isFetchingNext &&
        hasNextPage.value
      ) {
        fetchSongs(false);
      }
    },
    { root: null, rootMargin: "180px 0px", threshold: 0.1 },
  );
  observer.observe(node);
  onCleanup(() => observer.disconnect());
});

const handleCreate = async (payload) => {
  await createSong(payload);
  await fetchSongs(true);
  isCreateOpen.value = false;
};

const handleJoin = async (role) => {
  if (!selectedId.value) return;
  await joinSongRole(selectedId.value, role);
  await fetchDetails(selectedId.value);
  await fetchSongs(true);
};

const handleLeave = async (role) => {
  if (!selectedId.value) return;
  await leaveSongRole(selectedId.value, role);
  await fetchDetails(selectedId.value);
  await fetchSongs(true);
};

const handleUpdate = async (payload) => {
  if (!selectedId.value) return;
  await updateSong({ ...payload, id: selectedId.value });
  await fetchDetails(selectedId.value);
  await fetchSongs(true);
};

const handleDelete = async () => {
  if (!selectedId.value) return;
  await deleteSong(selectedId.value);
  selectedId.value = null;
  await fetchSongs(true);
};
</script>
