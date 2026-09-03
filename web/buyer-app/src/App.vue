<script setup lang="ts">
import { RouterView } from 'vue-router'
import Provider from './app/provider.vue'
</script>

<template>
  <Provider>
    <RouterView />
  </Provider>
</template>
