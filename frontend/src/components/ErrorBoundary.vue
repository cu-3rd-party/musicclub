<template>
  <div v-if="error" class="card error-boundary__card">
    <div class="card-title">Ошибка приложения</div>
    <div class="error-boundary__message">
      {{ error.message || "Неизвестная ошибка" }}
    </div>
    <pre class="error-boundary__stack">{{ error.stack }}</pre>
  </div>
  <slot v-else />
</template>

<script setup>
import { onErrorCaptured, ref } from "vue";
import "../styles/domains/errors.css";

const error = ref(null);

onErrorCaptured((err, _instance, info) => {
  console.error("App crash:", err, info);
  error.value = err;
  return false;
});
</script>
