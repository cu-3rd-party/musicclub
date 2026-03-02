<template>
  <div class="event-details__backdrop" @click="emit('close')">
    <div class="card event-details__card" @click.stop>
      <div class="section-header">
        <div class="card-title">
          <span role="img" aria-label="event">🚀</span>
          {{ evt?.title }}
        </div>
        <button class="button secondary" @click="emit('close')">Закрыть</button>
      </div>
      <div class="event-details__meta">
        {{ formatDate(timestampToDate(evt?.startAt)) }}
      </div>
      <div v-if="evt?.location" class="pill">{{ evt.location }}</div>

      <div class="event-details__section">
        <div class="card-title event-details__section-title">Участники</div>
        <div class="tags">
          <div
            v-for="p in participants"
            :key="p.role + p.user?.id"
            class="pill"
          >
            {{ p.user?.displayName }} — {{ p.role }}
          </div>
          <div v-if="participants.length === 0" class="event-details__empty">
            Пока пусто
          </div>
        </div>
      </div>

      <div class="event-details__section">
        <div class="card-title event-details__section-title">Треклист</div>
        <ol class="event-details__tracklist">
          <li v-for="item in data.tracklist?.items" :key="item.order">
            {{ item.customTitle || item.songId || "Трек" }}
            <span v-if="item.customArtist"> — {{ item.customArtist }}</span>
          </li>
          <div
            v-if="!data.tracklist?.items?.length"
            class="event-details__empty"
          >
            Не задан
          </div>
        </ol>
      </div>

      <form
        v-if="canEditEvents"
        class="grid event-details__form"
        @submit="handleSubmit"
      >
        <div class="card-title">Редактировать</div>
        <input class="input" v-model="form.title" />
        <input class="input" type="datetime-local" v-model="form.startAt" />
        <input class="input" v-model="form.location" placeholder="Локация" />
        <label class="event-details__checkbox">
          <input type="checkbox" v-model="form.notifyDayBefore" />
          Напомнить за день
        </label>
        <label class="event-details__checkbox">
          <input type="checkbox" v-model="form.notifyHourBefore" />
          Напомнить за час
        </label>
        <button class="button" type="submit">Сохранить</button>
      </form>

      <div v-if="canEditTracklists" class="event-details__tracklist-edit">
        <div class="card-title">Обновить треклист</div>
        <textarea
          class="textarea"
          rows="5"
          v-model="tracklistText"
          placeholder="1. Моя песня — Вокал&#10;2. Песня 2"
        />
        <button class="button" type="button" @click="handleTracklistSave">
          Сохранить треклист
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, reactive, ref, watch } from "vue";
import {
  formatDate,
  timestampToDate,
  toInputValue,
  toTimestamp,
} from "../utils/datetime";
import { parseTracklist } from "../utils/tracklist";
import "../styles/domains/events.css";

const props = defineProps({
  data: { type: Object, required: true },
  canEditEvents: { type: Boolean, default: false },
  canEditTracklists: { type: Boolean, default: false },
});

const emit = defineEmits(["close", "update", "setTracklist"]);

const evt = computed(() => props.data.event);
const participants = computed(() => props.data.participants ?? []);

const form = reactive({
  title: "",
  startAt: "",
  location: "",
  notifyDayBefore: false,
  notifyHourBefore: false,
});
const tracklistText = ref("");

watch(
  () => props.data,
  (value) => {
    const event = value.event;
    form.title = event?.title ?? "";
    form.startAt = event?.startAt
      ? toInputValue(timestampToDate(event.startAt))
      : "";
    form.location = event?.location ?? "";
    form.notifyDayBefore = event?.notifyDayBefore ?? false;
    form.notifyHourBefore = event?.notifyHourBefore ?? false;
    tracklistText.value = (value.tracklist?.items ?? [])
      .map((i) => `${i.order}. ${i.customTitle || i.songId || "Трек"}`)
      .join("\n");
  },
  { immediate: true },
);

const handleSubmit = (event) => {
  event.preventDefault();
  emit("update", {
    title: form.title,
    startAt: form.startAt ? toTimestamp(new Date(form.startAt)) : undefined,
    location: form.location,
    notifyDayBefore: form.notifyDayBefore,
    notifyHourBefore: form.notifyHourBefore,
  });
};

const handleTracklistSave = () => {
  emit("setTracklist", parseTracklist(tracklistText.value));
};
</script>
