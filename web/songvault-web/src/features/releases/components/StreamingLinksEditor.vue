<script setup lang="ts">
import { RELEASE_MAX_STREAMING_LINKS, STREAMING_URL_MAX } from '../types'
import { t } from '@/shared/i18n'

/**
 * #38 : les liens d'écoute d'une sortie, un champ par lien. La plateforme est reconnue par l'API à l'adresse :
 * il suffit de coller le lien que Spotify, Deezer ou Bandcamp donnent au bouton « Partager ».
 */
const links = defineModel<string[]>({ required: true })
defineProps<{ idPrefix: string; disabled?: boolean }>()

function add() {
  if (links.value.length < RELEASE_MAX_STREAMING_LINKS) links.value = [...links.value, '']
}

function update(index: number, value: string) {
  links.value = links.value.map((l, i) => (i === index ? value : l))
}

function remove(index: number) {
  links.value = links.value.filter((_, i) => i !== index)
}
</script>

<template>
  <fieldset class="bare links" :disabled="disabled">
    <legend>{{ t('releases.links.legend') }}</legend>
    <p class="muted"><small>{{ t('releases.links.hint') }}</small></p>
    <div v-for="(link, index) in links" :key="index" class="link-row">
      <input :id="`${idPrefix}-link-${index}`" :value="link" type="url" inputmode="url" :maxlength="STREAMING_URL_MAX"
             :placeholder="t('releases.links.placeholder')" :aria-label="t('releases.links.linkLabel', { number: index + 1 })"
             @input="update(index, ($event.target as HTMLInputElement).value)" />
      <button type="button" class="secondary" :aria-label="t('releases.links.removeLabel', { number: index + 1 })"
              @click="remove(index)">{{ t('releases.links.remove') }}</button>
    </div>
    <button v-if="links.length < RELEASE_MAX_STREAMING_LINKS" type="button" class="secondary" data-testid="add-link"
            @click="add">{{ t('releases.links.add') }}</button>
  </fieldset>
</template>

<style scoped>
.bare { border: 0; padding: 0; margin: 0; min-width: 0; }
.links { display: grid; gap: .4rem; }
.links legend { font-weight: 600; padding: 0; }
.links p { margin: 0; }
.link-row { display: flex; gap: .5rem; }
.link-row input { flex: 1; min-width: 0; }
.links > button { justify-self: start; }
</style>
