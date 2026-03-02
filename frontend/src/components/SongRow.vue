<template>
  <button
    class="button secondary song-row"
    :class="{ 'is-featured': isFeatured }"
    @click="emit('open')"
  >
    <div class="song-row__content">
      <img
        v-if="song.thumbnailUrl"
        :src="song.thumbnailUrl"
        :alt="song.title"
        class="song-row__thumb"
        @error="hideThumbnail"
      />
      <div class="song-row__text">
        <div class="song-row__title">
          {{ truncateString(song.title ?? "") }}
        </div>
        <div class="song-row__artist">
          {{ truncateString(song.artist ?? "") }}
        </div>
      </div>
      <div class="song-row__badge">
        <span class="song-row__count" :class="isFull ? 'is-full' : 'is-open'">
          {{ assignedCount }}/{{ totalRoles }}
        </span>
      </div>
    </div>
  </button>
</template>

<script setup>
import { computed } from "vue";
import "../styles/domains/songs.css";

const MAX_STRING_LEN = 36;

const props = defineProps({
  song: { type: Object, required: true },
});

const emit = defineEmits(["open"]);

const totalRoles = computed(() => props.song.availableRoles?.length || 0);
const assignedCount = computed(() => props.song.assignmentCount || 0);
const isFull = computed(() => assignedCount.value >= totalRoles.value);
const isFeatured = computed(() => Boolean(props.song.featured));

const truncateString = (value) => {
  if (value.length <= MAX_STRING_LEN) return value;
  return `${value.slice(0, MAX_STRING_LEN - 3)}...`;
};

const hideThumbnail = (event) => {
  const target = event.currentTarget;
  target?.classList.add("is-hidden");
};
</script>
