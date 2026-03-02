<template>
  <form class="grid create-event-form" @submit="handleSubmit">
    <div class="card-title">Создать мероприятие</div>
    <input class="input" placeholder="Название" v-model="form.title" required />
    <input
      class="input"
      type="datetime-local"
      v-model="form.startAt"
      placeholder="Дата/время"
    />
    <input
      class="input"
      placeholder="Локация (опционально)"
      v-model="form.location"
    />
    <label class="create-event-form__checkbox">
      <input type="checkbox" v-model="form.notifyDayBefore" />
      Напомнить за день
    </label>
    <label class="create-event-form__checkbox">
      <input type="checkbox" v-model="form.notifyHourBefore" />
      Напомнить за час
    </label>
    <textarea
      class="textarea"
      rows="4"
      placeholder="1. Название — Исполнитель&#10;2. Следующий трек"
      v-model="form.tracklistText"
    />
    <button class="button" type="submit" :disabled="isSaving">
      {{ isSaving ? "Создаем…" : "Создать" }}
    </button>
    <div v-if="error" class="create-event-form__error">{{ error }}</div>
  </form>
</template>

<script setup>
import { reactive, ref } from "vue";
import { toTimestamp } from "../../utils/datetime";
import { parseTracklist } from "../../utils/tracklist";
import "../../styles/domains/events.css";

const props = defineProps({
  onSubmit: { type: Function, required: true },
});

const form = reactive({
  title: "",
  startAt: "",
  location: "",
  notifyDayBefore: false,
  notifyHourBefore: false,
  tracklistText: "",
});
const isSaving = ref(false);
const error = ref(null);

const handleSubmit = async (event) => {
  event.preventDefault();
  isSaving.value = true;
  error.value = null;
  try {
    await props.onSubmit({
      title: form.title,
      startAt: form.startAt ? toTimestamp(new Date(form.startAt)) : undefined,
      location: form.location,
      notifyDayBefore: form.notifyDayBefore,
      notifyHourBefore: form.notifyHourBefore,
      tracklist: form.tracklistText
        ? parseTracklist(form.tracklistText)
        : undefined,
    });
    form.title = "";
    form.startAt = "";
    form.location = "";
    form.notifyDayBefore = false;
    form.notifyHourBefore = false;
    form.tracklistText = "";
  } catch (err) {
    error.value = err.message;
  } finally {
    isSaving.value = false;
  }
};
</script>
