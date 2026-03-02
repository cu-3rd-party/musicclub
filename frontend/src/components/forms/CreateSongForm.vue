<template>
  <form class="grid create-song-form" @submit="handleSubmit">
    <div class="card-title">Добавить песню</div>
    <input class="input" placeholder="Название" v-model="form.title" required />
    <input
      class="input"
      placeholder="Исполнитель"
      v-model="form.artist"
      required
    />
    <textarea
      class="textarea"
      placeholder="Описание"
      v-model="form.description"
    />
    <div class="create-song-form__link-row">
      <input
        class="input"
        placeholder="Ссылка"
        v-model="form.linkUrl"
        required
      />
      <select class="select" v-model.number="form.linkKind">
        <option :value="1">YouTube</option>
        <option :value="2">Яндекс Музыка</option>
        <option :value="3">Soundcloud</option>
      </select>
    </div>
    <input
      class="input"
      placeholder="Роли через запятую"
      v-model="form.roles"
    />
    <label v-if="canFeature" class="create-song-form__checkbox">
      <input type="checkbox" v-model="form.featured" />
      Сделать featured
    </label>
    <button class="button" type="submit" :disabled="isSaving">
      {{ isSaving ? "Сохраняем…" : "Создать" }}
    </button>
    <div v-if="error" class="create-song-form__error">{{ error }}</div>
  </form>
</template>

<script setup>
import { reactive, ref } from "vue";
import "../../styles/domains/songs.css";

const props = defineProps({
  canFeature: { type: Boolean, default: false },
  onSubmit: { type: Function, required: true },
});

const form = reactive({
  title: "",
  artist: "",
  description: "",
  linkUrl: "",
  linkKind: 1,
  roles: "вокал, гитара, бас, барабаны",
  thumbnailUrl: "",
  featured: false,
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
      artist: form.artist,
      description: form.description,
      linkUrl: form.linkUrl,
      linkKind: form.linkKind,
      roles: form.roles
        .split(",")
        .map((r) => r.trim())
        .filter(Boolean),
      thumbnailUrl: form.thumbnailUrl,
      featured: props.canFeature ? form.featured : undefined,
    });
    form.title = "";
    form.artist = "";
    form.description = "";
    form.linkUrl = "";
    form.thumbnailUrl = "";
    form.featured = false;
  } catch (err) {
    error.value = err.message;
  } finally {
    isSaving.value = false;
  }
};
</script>
