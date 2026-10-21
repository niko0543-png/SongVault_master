<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { sharingApi } from '../api/sharingApi'
import { SHARE_PASSWORD_MAX, type SharedContent } from '../types'
import { releaseTypeLabels } from '@/features/releases/types'
import { formatReleaseDate } from '@/features/releases/format'
import LoadingState from '@/shared/components/LoadingState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

const props = defineProps<{ token: string }>()

const state = ref<'loading' | 'locked' | 'ready' | 'not-found' | 'error'>('loading')
const content = ref<SharedContent | null>(null)
const errorMessage = ref('')

async function load() {
  state.value = 'loading'
  try {
    content.value = await sharingApi.open(props.token)
    state.value = 'ready'
    document.title = `${content.value.title} · ${content.value.bandName} · SongVault`
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) state.value = 'locked'
    else if (e instanceof ApiError && e.status === 404) state.value = 'not-found'
    else {
      errorMessage.value = getErrorMessage(e)
      state.value = 'error'
    }
  }
}
watch(() => props.token, load, { immediate: true })

// ---- Mot de passe ----
const password = ref('')
const unlocking = ref(false)
const unlockError = ref<string | null>(null)

async function onUnlock() {
  if (!password.value) return
  unlocking.value = true
  unlockError.value = null
  try {
    await sharingApi.unlock(props.token, password.value)
    password.value = ''
    await load()
  } catch (e) {
    // 422 : mauvais mot de passe ; 429 : trop d'essais (le détail de l'API le dit)
    unlockError.value = e instanceof ApiError && e.status === 422 ? t('sharing.shared.wrongPassword') : getErrorMessage(e)
  } finally {
    unlocking.value = false
  }
}

const subtitle = computed(() => {
  const c = content.value
  if (!c) return ''
  if (c.targetType === 'Release' && c.releaseType)
    return `${releaseTypeLabels[c.releaseType]} · ${formatReleaseDate(c.releaseDate)}`
  const track = c.tracks[0]
  return track ? `v${track.versionNumber} · ${track.versionTitle}` : ''
})

// Un lien privé n'a rien à faire dans un moteur de recherche (l'API envoie aussi X-Robots-Tag)
let robots: HTMLMetaElement | null = null
onMounted(() => {
  robots = document.createElement('meta')
  robots.name = 'robots'
  robots.content = 'noindex, nofollow'
  document.head.appendChild(robots)
})
onBeforeUnmount(() => robots?.remove())
</script>

<template>
  <section class="shared">
    <LoadingState v-if="state === 'loading'" />

    <div v-else-if="state === 'locked'" class="card">
      <h1>{{ t('sharing.shared.protected') }}</h1>
      <form novalidate @submit.prevent="onUnlock">
        <div class="field">
          <label for="share-password">{{ t('sharing.shared.password') }}</label>
          <input id="share-password" v-model="password" type="password" autocomplete="off" required
                 :maxlength="SHARE_PASSWORD_MAX" aria-describedby="share-password-help" />
          <small id="share-password-help" class="muted">{{ t('sharing.shared.passwordHelp') }}</small>
        </div>
        <p v-if="unlockError" class="field-error" role="alert">{{ unlockError }}</p>
        <button type="submit" :disabled="unlocking || !password">{{ unlocking ? t('sharing.shared.checking') : t('sharing.shared.listen') }}</button>
      </form>
    </div>

    <div v-else-if="state === 'not-found'" class="card" role="alert">
      <h1>{{ t('sharing.shared.unavailable') }}</h1>
      <p>{{ t('sharing.shared.unavailableText') }}</p>
    </div>

    <div v-else-if="state === 'error'" class="card" role="alert">
      <p>{{ errorMessage }}</p>
      <button type="button" class="secondary" @click="load">{{ t('common.actions.retry') }}</button>
    </div>

    <template v-else-if="content">
      <header class="shared-header">
        <img v-if="content.hasCover" :src="sharingApi.coverUrl(token)" :alt="t('sharing.shared.coverOf', { title: content.title })" class="cover" />
        <div>
          <p class="muted">{{ t('sharing.shared.presents', { bandName: content.bandName }) }}</p>
          <h1>{{ content.title }}</h1>
          <p class="muted">{{ subtitle }}</p>
        </div>
      </header>

      <ol class="tracks">
        <li v-for="track in content.tracks" :key="track.number" class="card track" data-testid="shared-track">
          <h2>
            <span v-if="content.targetType === 'Release'" class="number">{{ track.number }}.</span>
            {{ track.songTitle }}<small v-if="track.artist" class="muted"> · {{ track.artist }}</small>
          </h2>
          <p v-if="content.targetType === 'Release'" class="muted">v{{ track.versionNumber }} · {{ track.versionTitle }}</p>
          <p v-if="track.files.length === 0" class="muted">{{ t('sharing.shared.noAudio') }}</p>
          <figure v-for="file in track.files" :key="file.id" class="file">
            <figcaption>{{ file.fileName }}</figcaption>
            <audio controls preload="none" controlslist="nodownload" :src="sharingApi.fileUrl(token, file.id)"
                   :aria-label="t('sharing.shared.listenTo', { fileName: file.fileName })" />
          </figure>
        </li>
      </ol>

      <p class="muted notice">
        {{ t('sharing.shared.listenOnly', { expiresAt: formatDate(content.expiresAt) }) }}
      </p>
    </template>
  </section>
</template>

<style scoped>
.shared { max-width: 40rem; margin: 0 auto; }
.shared-header { display: flex; gap: 1rem; align-items: center; margin-bottom: 1rem; }
.shared-header h1 { margin: .25rem 0; }
.shared-header p { margin: 0; }
.cover { width: 8rem; aspect-ratio: 1; object-fit: cover; border-radius: var(--radius); }
.tracks { list-style: none; padding: 0; display: grid; gap: .75rem; }
.track h2 { font-size: 1.05rem; margin: 0 0 .25rem; }
.number { color: var(--muted); }
.file { margin: .5rem 0 0; }
.file figcaption { font-size: .9rem; margin-bottom: .25rem; overflow-wrap: anywhere; }
.file audio { width: 100%; }
.notice { font-size: .9rem; }
@media (max-width: 600px) { .shared-header { flex-direction: column; align-items: flex-start; } }
</style>