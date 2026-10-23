<script setup lang="ts">
import { streamingPlatformNames, type StreamingLink } from '../types'
import { t } from '@/shared/i18n'

/** #38 : les liens d'écoute d'une sortie, à ouvrir dans un nouvel onglet. */
defineProps<{ links: StreamingLink[] }>()

function nameOf(link: StreamingLink): string {
  if (link.platform !== 'Other') return streamingPlatformNames[link.platform]
  try {
    return new URL(link.url).hostname.replace(/^www\./, '')
  } catch {
    return t('releases.links.other')
  }
}
</script>

<template>
  <ul v-if="links.length" class="streaming-links" :aria-label="t('releases.links.listLabel')">
    <li v-for="link in links" :key="link.url">
      <a :href="link.url" target="_blank" rel="noopener noreferrer" :class="['platform', link.platform.toLowerCase()]"
         data-testid="streaming-link">▶ {{ nameOf(link) }}</a>
    </li>
  </ul>
</template>

<style scoped>
.streaming-links { list-style: none; padding: 0; margin: .5rem 0; display: flex; flex-wrap: wrap; gap: .5rem; }
.platform {
  display: inline-block; padding: .3rem .75rem; border: 1px solid var(--primary); border-radius: 999px;
  text-decoration: none; font-size: .9rem;
}
</style>
