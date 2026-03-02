<template>
  <div class="card">
    <div class="section-header">
      <div class="card-title">
        <span role="img" aria-label="calendar">📅</span>
        Мероприятия
      </div>
    </div>

    <div v-if="listState.isLoading">Загружаем мероприятия…</div>
    <div v-if="listState.error" class="event-list__error">
      Ошибка: {{ listState.error.message }}
    </div>

    <div v-if="listState.items.length > 0" class="grid">
      <button
        v-for="evt in listState.items"
        :key="evt.id"
        class="button secondary event-list__item-button"
        @click="selectedId = evt.id"
      >
        <div class="event-list__item-title">{{ evt.title }}</div>
        <div class="event-list__item-meta">
          {{ formatDate(timestampToDate(evt.startAt)) }}
        </div>
        <div v-if="evt.location" class="event-list__item-location">
          {{ evt.location }}
        </div>
      </button>
    </div>

    <teleport v-if="canEditEvents" to="body">
      <div class="event-fab-wrap">
        <div v-if="isCreateOpen" class="card event-create-dialog">
          <div class="section-header">
            <div class="card-title">Новое событие</div>
            <button
              class="button secondary"
              type="button"
              @click="isCreateOpen = false"
            >
              Закрыть
            </button>
          </div>
          <CreateEventForm :on-submit="handleCreate" />
        </div>
        <button
          class="song-fab"
          type="button"
          @click="isCreateOpen = !isCreateOpen"
        >
          {{ isCreateOpen ? "×" : "+" }}
        </button>
      </div>
    </teleport>

    <EventDetailsCard
      v-if="selectedId && details && !isDetailLoading && !detailError"
      :key="selectedId"
      :data="details"
      :can-edit-events="canEditEvents"
      :can-edit-tracklists="canEditTracklists"
      @close="selectedId = null"
      @update="handleUpdate"
      @set-tracklist="handleSetTracklist"
    />
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref, watch } from "vue";
import {
  createEvent,
  getEvent,
  listEvents,
  setTracklist,
  updateEvent,
} from "../services/api";
import CreateEventForm from "./forms/CreateEventForm.vue";
import EventDetailsCard from "./EventDetailsCard.vue";
import { formatDate, timestampToDate } from "../utils/datetime";
import "../styles/domains/events.css";

const props = defineProps({
  permissions: { type: Object, default: undefined },
});

const selectedId = ref(null);
const listState = reactive({ items: [], isLoading: false, error: null });
const details = ref(null);
const detailError = ref(null);
const isDetailLoading = ref(false);
const isCreateOpen = ref(false);

const fetchEvents = async () => {
  listState.isLoading = true;
  listState.error = null;
  try {
    const res = await listEvents();
    listState.items = res.events ?? [];
  } catch (err) {
    listState.items = [];
    listState.error = err;
  } finally {
    listState.isLoading = false;
  }
};

onMounted(() => {
  fetchEvents();
});

const fetchDetails = async (eventId) => {
  isDetailLoading.value = true;
  detailError.value = null;
  try {
    const res = await getEvent(eventId);
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

const canEditEvents = computed(() =>
  Boolean(props.permissions?.events?.editEvents),
);
const canEditTracklists = computed(() =>
  Boolean(
    props.permissions?.events?.editTracklists ||
    props.permissions?.events?.editEvents,
  ),
);

const handleCreate = async (payload) => {
  await createEvent(payload);
  await fetchEvents();
  isCreateOpen.value = false;
};

const handleUpdate = async (payload) => {
  if (!selectedId.value) return;
  await updateEvent({ ...payload, id: selectedId.value });
  await fetchDetails(selectedId.value);
  await fetchEvents();
};

const handleSetTracklist = async (items) => {
  if (!selectedId.value) return;
  await setTracklist(selectedId.value, items);
  await fetchDetails(selectedId.value);
};
</script>
