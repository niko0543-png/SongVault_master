<script setup lang="ts">
import { LOCALES, isLocale, locale, t, type Locale } from '@/shared/i18n'

/** #26 : choix de la langue, dans le pied de page ; la page prévient le compte (AppLayout). */
const emit = defineEmits<{ change: [locale: Locale] }>()

function onChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (isLocale(value) && value !== locale.value) emit('change', value)
}
</script>

<template>
  <select class="locale-switcher" :value="locale" :aria-label="t('common.language.label')" @change="onChange">
    <option v-for="item in LOCALES" :key="item.code" :value="item.code" :lang="item.code">{{ item.label }}</option>
  </select>
</template>

<style scoped>
.locale-switcher { width: auto; padding: .25rem .5rem; font-size: inherit; }
</style>