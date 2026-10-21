<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { rightsApi } from '../api/rightsApi'
import { CODE_INPUT_MAX, creditRoleLabels, type SongRights, type VersionIsrc } from '../types'
import { creditsError, formatPercent, toCents, toCredits, toRows, type CreditRow } from '../rules'
import CreditsEditor from './CreditsEditor.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

/**
 * Crédits et codes d'un morceau (#22, ADR 0029) : ISWC de l'œuvre, ayants droit et leurs parts, ISRC de chaque
 * version, export CSV. versionCount change quand la page crée une version : on recharge alors la liste des ISRC.
 */
const props = defineProps<{ songId: string; readonly: boolean; versionCount: number }>()
const toast = useToast()

const rights = ref<SongRights | null>(null)
const isLoading = ref(true)
const loadError = ref<unknown>(null)
const iswc = ref('')
const rows = ref<CreditRow[]>([])
const isrcDrafts = reactive<Record<string, string>>({})

function reset(value: SongRights) {
  rights.value = value
  iswc.value = value.iswc ?? ''
  rows.value = toRows(value.credits)
  setVersions(value.versions)
}

function setVersions(versions: VersionIsrc[]) {
  if (rights.value) rights.value = { ...rights.value, versions }
  for (const v of versions) isrcDrafts[v.id] = v.isrc ?? ''
}

async function load(id: string) {
  isLoading.value = true
  loadError.value = null
  try {
    reset(await rightsApi.get(id))
  } catch (e) {
    loadError.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => props.songId, load, { immediate: true })

// Une version vient d'être créée : seule la liste des ISRC change, les crédits en cours de saisie restent
watch(() => props.versionCount, async () => {
  try {
    setVersions((await rightsApi.get(props.songId)).versions)
  } catch {
    /* la liste sera à jour au prochain chargement */
  }
})

// ---- Crédits et ISWC ----
const problem = computed(() => creditsError(rows.value))
const saving = useAsyncAction(() =>
  rightsApi.save(props.songId, { iswc: iswc.value.trim() || null, credits: toCredits(rows.value) }))

async function onSave() {
  if (problem.value) return
  const saved = await saving.run()
  if (!saved) return
  reset(saved)
  toast.success(t('rights.section.saved'))
}

// ---- ISRC d'une version ----
const isrcSaving = ref<string | null>(null)
const isrcError = ref<{ id: string; message: string } | null>(null)

async function onSaveIsrc(version: VersionIsrc) {
  isrcSaving.value = version.id
  isrcError.value = null
  try {
    const saved = await rightsApi.setIsrc(props.songId, version.id, isrcDrafts[version.id]?.trim() || null)
    setVersions(rights.value!.versions.map((v) => (v.id === saved.id ? saved : v)))
  } catch (e) {
    isrcError.value = { id: version.id, message: getErrorMessage(e) }
  } finally {
    isrcSaving.value = null
  }
}

const savedTotal = computed(() => formatPercent(toCents(rights.value?.totalPercent ?? 0)))
</script>

<template>
  <SectionCard :title="t('rights.section.title')">
    <template #actions>
      <a v-if="rights" :href="rightsApi.songCsvUrl(songId)" download>{{ t('rights.section.export') }}</a>
    </template>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="loadError" :message="getErrorMessage(loadError)" @retry="load(songId)" />

    <template v-else-if="rights">
      <!-- Lecture seule (Guest) -->
      <template v-if="readonly">
        <p><strong>ISWC</strong> {{ t('rights.section.iswc', { iswc: rights.iswc ?? t('rights.section.notSet') }) }}</p>
        <p v-if="rights.credits.length === 0" class="muted">{{ t('rights.section.empty') }}</p>
        <table v-else class="credits">
          <thead><tr><th scope="col">{{ t('rights.section.holder') }}</th><th scope="col">{{ t('rights.section.role') }}</th><th scope="col" class="num">{{ t('rights.section.share') }}</th></tr></thead>
          <tbody>
            <tr v-for="(c, i) in rights.credits" :key="i">
              <td>{{ c.name }}</td><td>{{ creditRoleLabels[c.role] }}</td><td class="num">{{ formatPercent(toCents(c.sharePercent)) }}</td>
            </tr>
          </tbody>
          <tfoot><tr><th scope="row" colspan="2">{{ t('rights.section.total') }}</th><td class="num">{{ savedTotal }}</td></tr></tfoot>
        </table>
      </template>

      <!-- Édition (Member et Owner) -->
      <form v-else @submit.prevent="onSave">
        <div class="field">
          <label for="song-iswc">ISWC <small class="muted">{{ t('rights.section.iswcHelp') }}</small></label>
          <input id="song-iswc" v-model="iswc" :maxlength="CODE_INPUT_MAX" autocomplete="off" placeholder="T-000.000.000-0" />
        </div>
        <CreditsEditor v-model="rows" :disabled="saving.isLoading.value" />
        <p v-if="saving.error.value" class="field-error" role="alert">{{ getErrorMessage(saving.error.value) }}</p>
        <button type="submit" :disabled="!!problem || saving.isLoading.value">{{ t('rights.section.save') }}</button>
      </form>

      <h3>{{ t('rights.section.isrcTitle') }} <small class="muted">{{ t('rights.section.isrcHelp') }}</small></h3>
      <p v-if="rights.versions.length === 0" class="muted">{{ t('rights.section.noVersion') }}</p>
      <ul class="isrc-list">
        <li v-for="v in rights.versions" :key="v.id" data-testid="version-isrc">
          <span class="version">v{{ v.number }} · {{ v.title }}</span>
          <template v-if="readonly">
            <span>{{ v.isrc ?? t('rights.section.notSet') }}</span>
          </template>
          <form v-else class="isrc-form" @submit.prevent="onSaveIsrc(v)">
            <input v-model="isrcDrafts[v.id]" :maxlength="CODE_INPUT_MAX" :aria-label="t('rights.section.isrcOf', { number: v.number })"
                   autocomplete="off" placeholder="FR-XXX-26-00001" />
            <button type="submit" class="secondary" :disabled="isrcSaving === v.id">{{ t('common.actions.save') }}</button>
          </form>
          <p v-if="isrcError?.id === v.id" class="field-error" role="alert">{{ isrcError.message }}</p>
        </li>
      </ul>
    </template>
  </SectionCard>
</template>

<style scoped>
.credits { width: 100%; border-collapse: collapse; margin-bottom: 1rem; }
.credits th, .credits td { text-align: left; padding: .25rem .5rem; border-bottom: 1px solid var(--border); }
.credits .num { text-align: right; }
h3 { font-size: 1rem; margin: 1.25rem 0 .5rem; }
.isrc-list { list-style: none; padding: 0; margin: 0; }
.isrc-list li { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 18rem); gap: .5rem; align-items: center; margin-bottom: .5rem; }
.isrc-list .field-error { grid-column: 1 / -1; margin: 0; }
.isrc-form { display: flex; gap: .5rem; }
.isrc-form input { flex: 1; min-width: 0; }
@media (max-width: 640px) { .isrc-list li { grid-template-columns: 1fr; } }
</style>