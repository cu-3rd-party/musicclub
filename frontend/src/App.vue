<template>
  <ErrorBoundary>
    <div class="app-shell">
      <AuthGate />
    </div>
  </ErrorBoundary>
</template>

<script setup>
import ErrorBoundary from "./components/ErrorBoundary.vue";
import AuthGate from "./components/AuthGate.vue";
</script>
