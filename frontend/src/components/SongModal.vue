<template>
  <teleport to="body">
    <div class="modal-backdrop" @click="emit('close')">
      <div class="card modal-window" @click.stop>
        <div class="section-header">
          <div class="card-title">
            <span role="img" aria-label="note">🎶</span>
            {{ song?.title }}
          </div>
          <button class="button secondary" @click="emit('close')">
            Закрыть
          </button>
        </div>
        <div class="scroll-area">
          <div class="song-modal__artist">{{ song?.artist }}</div>
          <img
            v-if="song?.thumbnailUrl"
            :src="song.thumbnailUrl"
            :alt="song.title"
            class="song-modal__thumbnail"
            @error="hideThumbnail"
          />
          <a
            v-if="song?.link?.url"
            :href="song.link.url"
            target="_blank"
            rel="noreferrer"
            class="pill"
          >
            {{ linkLabel }}
          </a>
          <p v-if="song?.description" class="song-modal__description">
            {{ song.description }}
          </p>

          <div class="song-modal__section">
            <div class="card-title song-modal__section-title">
              <span>Роли</span>
              <span
                class="song-modal__role-count"
                :class="isFull ? 'is-full' : 'is-open'"
              >
                {{ filledRoleCount }}/{{ totalRoles }}
              </span>
            </div>
            <div class="tags">
              <div
                v-for="(role, index) in song?.availableRoles"
                :key="`${role}-${index}`"
                class="pill song-modal__role-pill"
                :class="{ 'is-mine': isRoleMine(role) }"
              >
                <div class="song-modal__role-info">
                  <div class="song-modal__role-title">{{ role }}</div>
                  <div class="song-modal__role-members">
                    {{ roleMembers(role) }}
                  </div>
                </div>
                <button
                  v-if="isRoleMine(role)"
                  class="button secondary"
                  @click="emit('leave', role)"
                >
                  Снять участие
                </button>
                <button
                  v-else
                  class="button"
                  :disabled="isFull"
                  @click="emit('join', role)"
                >
                  Присоединиться
                </button>
              </div>
            </div>
          </div>

          <div class="song-modal__section">
            <div
              class="card-title song-modal__section-title song-modal__section-title--wide"
            >
              <span>Участники</span>
              <button
                v-if="canEditAny"
                class="button secondary"
                type="button"
                @click="handleCopyMentions"
              >
                Скопировать теги
              </button>
            </div>
            <div class="grid">
              <div
                v-for="a in assignments"
                :key="a.role + a.user?.id"
                class="pill"
              >
                {{ a.user?.displayName }} — {{ a.role }}
              </div>
              <div v-if="assignments.length === 0" class="song-modal__empty">
                Пока пусто
              </div>
            </div>
          </div>

          <div v-if="canEdit" class="song-modal__edit">
            <button class="button secondary" @click="isEditing = !isEditing">
              {{ isEditing ? "Скрыть форму" : "Редактировать" }}
            </button>
            <form
              v-if="isEditing"
              class="grid song-modal__edit-form"
              @submit="handleSubmit"
            >
              <input
                class="input"
                v-model="form.title"
                placeholder="Название"
                required
              />
              <input
                class="input"
                v-model="form.artist"
                placeholder="Исполнитель"
                required
              />
              <textarea
                class="textarea"
                v-model="form.description"
                placeholder="Описание"
              />
              <input
                class="input"
                v-model="form.linkUrl"
                placeholder="Ссылка"
                required
              />
              <select class="select" v-model.number="form.linkKind">
                <option :value="1">YouTube</option>
                <option :value="2">Яндекс Музыка</option>
                <option :value="3">Soundcloud</option>
              </select>
              <input
                class="input"
                v-model="form.rolesText"
                placeholder="Роли через запятую"
              />
              <label v-if="canFeature" class="song-modal__feature-label">
                <input type="checkbox" v-model="form.featured" />
                Featured
              </label>
              <div class="song-modal__edit-actions">
                <button class="button" type="submit">Сохранить</button>
                <button
                  class="button danger"
                  type="button"
                  @click="emit('delete')"
                >
                  Удалить песню
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  </teleport>
</template>

<script setup>
import {
  computed,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
} from "vue";
import "../styles/domains/songs.css";

const props = defineProps({
  details: { type: Object, required: true },
  canEdit: { type: Boolean, default: false },
  canEditAny: { type: Boolean, default: false },
  currentUserId: { type: String, required: true },
});

const emit = defineEmits(["close", "join", "leave", "update", "delete"]);

const song = computed(() => props.details.song);
const assignments = computed(() => props.details.assignments ?? []);
const canFeature = computed(() =>
  Boolean(props.details.permissions?.songs?.editFeaturedSongs),
);

const isEditing = ref(false);
const form = reactive({
  title: "",
  artist: "",
  description: "",
  linkUrl: "",
  linkKind: 0,
  rolesText: "",
  thumbnailUrl: "",
  featured: false,
});

const filledRoleCount = computed(
  () => new Set(assignments.value.map((a) => a.role)).size,
);
const totalRoles = computed(() => song.value?.availableRoles?.length || 0);
const isFull = computed(() => filledRoleCount.value >= totalRoles.value);

watch(
  () => props.details,
  (value) => {
    const nextSong = value.song;
    form.title = nextSong?.title ?? "";
    form.artist = nextSong?.artist ?? "";
    form.description = nextSong?.description ?? "";
    form.linkUrl = nextSong?.link?.url ?? "";
    form.linkKind = nextSong?.link?.kind ?? 0;
    form.rolesText = (nextSong?.availableRoles ?? []).join(", ");
    form.thumbnailUrl = nextSong?.thumbnailUrl ?? "";
    form.featured = nextSong?.featured ?? false;
    isEditing.value = false;
  },
  { immediate: true },
);

const linkLabel = computed(() => {
  const map = {
    1: "YouTube",
    2: "Я.Музыка",
    3: "Soundcloud",
  };
  return map[form.linkKind] ?? "Ссылка";
});

const handleSubmit = (event) => {
  event.preventDefault();
  const roles = form.rolesText
    .split(",")
    .map((role) => role.trim())
    .filter(Boolean);
  emit("update", {
    title: form.title,
    artist: form.artist,
    description: form.description,
    linkUrl: form.linkUrl,
    linkKind: form.linkKind,
    roles,
    thumbnailUrl: form.thumbnailUrl,
    featured: canFeature.value ? form.featured : undefined,
  });
  isEditing.value = false;
};

const handleCopyMentions = async () => {
  const title = song.value?.title ?? "";
  const mentions = assignments.value
    .map(
      (assignment) => assignment.user?.username || assignment.user?.displayName,
    )
    .filter(Boolean);
  const uniqueMentions = Array.from(new Set(mentions));
  const mentionLines = uniqueMentions.map((name) =>
    name.startsWith("@") ? name : `@${name}`,
  );
  const payload = `**${title}:**\n${mentionLines.join("\n")}`;
  try {
    await navigator.clipboard.writeText(payload);
  } catch (error) {
    console.error("Failed to copy song info", error);
  }
};

const roleMembers = (role) => {
  const members = assignments.value.filter((a) => a.role === role);
  return members.length === 0
    ? "Свободно"
    : members.map((m) => m.user?.displayName).join(", ");
};

const isRoleMine = (role) => {
  const members = assignments.value.filter((a) => a.role === role);
  return members.some((m) => m.user?.id === props.currentUserId);
};

const hideThumbnail = (event) => {
  const target = event.currentTarget;
  target?.classList.add("is-hidden");
};

let previousOverflow = "";
onMounted(() => {
  const { style } = document.body;
  previousOverflow = style.overflow;
  style.overflow = "hidden";
});
onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow;
});
</script>
