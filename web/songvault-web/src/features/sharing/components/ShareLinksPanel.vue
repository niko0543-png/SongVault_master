<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { sharingApi } from '../api/sharingApi'
import {
  accessKindLabels, SHARE_DEFAULT_DURATION, SHARE_DURATIONS, SHARE_LABEL_MAX, SHARE_PASSWORD_MAX, SHARE_PASSWORD_MIN,
  shareStatusLabels, type ShareLink, type ShareLinkAccess, type ShareLinkCreated, type ShareTarget,
} from '../types'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

const props = defineProps<{ target: ShareTarget }>()
const toast = useToast()
const { confirm } = useConfirm()

const form = reactive({ label: '', days: SHARE_DEFAULT_DURATION, password: '' })
const links = ref<ShareLink[]>([])
const created = ref<ShareLinkCreated | null>(null)
const listError = ref<string | null>(null)
const busyId = ref<string | null>(null)
const openJournalId = ref<string | null>(null)
const journal = ref<ShareLinkAccess[]>([])
const journalError = ref<string | null>(null)

const passwordError = computed(() =>
  form.password && form.password.length < SHARE_PASSWORD_MIN ? t('sharing.panel.passwordMin', { sharePasswordMin: SHARE_PASSWORD_MIN }) : null)
const { isLoading, error, run } = useAsyncAction(sharingApi.create)
const createMessage = computed(() => (error.value ? getErrorMessage(error.value) : null))

async function load() {
  listError.value = null
  try {
    links.value = await sharingApi.list(props.target)
  } catch (e) {
    listError.value = getErrorMessage(e)
  }
}
// Autre version (navigation v1 → v2) ou autre sortie sans quitter la page : autre liste, et le lien affiché n'est plus le bon
watch(() => `${props.target.type}:${props.target.id}`, () => {
  created.value = null
  openJournalId.value = null
  void load()
}, { immediate: true })

async function onCreate() {
  if (passwordError.value) return
  const result = await run(props.target, {
    label: form.label.trim() || null,
    expiresInDays: form.days,
    password: form.password || null,
  })
  if (!result) return
  created.value = result
  form.label = ''
  form.password = ''
  await load()
}

async function onCopy() {
  if (!created.value) return
  try {
    await navigator.clipboard.writeText(created.value.url)
    toast.success(t('sharing.panel.copied'))
  } catch {
    // Presse-papiers indisponible (page servie en http hors localhost) : le champ reste sélectionnable
    toast.error(t('sharing.panel.copyFailed'))
  }
}

async function onRevoke(link: ShareLink) {
  busyId.value = link.id
  try {
    await sharingApi.revoke(link.id)
    toast.success(t('sharing.panel.revoked'))
    if (created.value?.link.id === link.id) created.value = null
    await load()
  } catch (e) {
    toast.error(getErrorMessage(e))
  } finally {
    busyId.value = null
  }
}

async function onDelete(link: ShareLink) {
  const confirmed = await confirm({
    title: t('sharing.panel.deleteTitle'),
    message: t('sharing.panel.deleteMessage'),
    confirmLabel: t('common.actions.delete'),
    danger: true,
  })
  if (!confirmed) return
  busyId.value = link.id
  try {
    await sharingApi.remove(link.id)
    if (created.value?.link.id === link.id) created.value = null
    if (openJournalId.value === link.id) openJournalId.value = null
    await load()
  } catch (e) {
    toast.error(getErrorMessage(e))
  } finally {
    busyId.value = null
  }
}

async function onToggleJournal(link: ShareLink) {
  if (openJournalId.value === link.id) {
    openJournalId.value = null
    return
  }
  openJournalId.value = link.id
  journal.value = []
  journalError.value = null
  try {
    journal.value = await sharingApi.accesses(link.id)
  } catch (e) {
    journalError.value = getErrorMessage(e)
  }
}

function statusLine(link: ShareLink): string {
  if (link.status === 'Revoked' && link.revokedAt) return t('sharing.panel.revokedOn', { revokedAt: formatDate(link.revokedAt) })
  if (link.status === 'Expired') return t('sharing.panel.expiredOn', { expiresAt: formatDate(link.expiresAt) })
  return t('sharing.panel.expiresOn', { expiresAt: formatDate(link.expiresAt) })
}

function opensLine(link: ShareLink): string {
  if (link.openCount === 0) return t('sharing.panel.neverOpened')
  const times = t('sharing.panel.opens', { count: link.openCount })
  return link.lastOpenedAt ? t('sharing.panel.lastOpened', { times, date: formatDate(link.lastOpenedAt) }) : times
}
</script>

<template>
  <SectionCard :title="t('sharing.panel.title')">
    <p class="muted">
      {{ target.type === 'Version' ? t('sharing.panel.introVersion') : t('sharing.panel.introRelease') }}
    </p>
    <form class="share-form" novalidate @submit.prevent="onCreate">
      <div class="field">
        <label for="share-label">{{ t('sharing.panel.forWhom') }}</label>
        <input id="share-label" v-model="form.label" :maxlength="SHARE_LABEL_MAX" :placeholder="t('sharing.panel.labelPlaceholder')" autocomplete="off" />
      </div>
      <div class="field">
        <label for="share-days">{{ t('sharing.panel.validFor') }}</label>
        <select id="share-days" v-model.number="form.days">
          <option v-for="days in SHARE_DURATIONS" :key="days" :value="days">{{ t('sharing.panel.days', { count: days }) }}</option>
        </select>
      </div>
      <div class="field">
        <label for="share-password">{{ t('sharing.panel.password') }}</label>
        <input id="share-password" v-model="form.password" type="password" autocomplete="new-password"
               :maxlength="SHARE_PASSWORD_MAX" :aria-invalid="!!passwordError" aria-describedby="share-password-error" />
        <p v-if="passwordError" id="share-password-error" class="field-error">{{ passwordError }}</p>
      </div>
      <button type="submit" :disabled="isLoading || !!passwordError">{{ isLoading ? t('sharing.panel.creating') : t('sharing.panel.create') }}</button>
    </form>
    <p v-if="createMessage" class="field-error" role="alert">{{ createMessage }}</p>

    <div v-if="created" class="created" data-testid="share-link-url">
      <p class="muted">
        {{ t('sharing.panel.copyNow', { expiresAt: formatDate(created.link.expiresAt) }) }}
        <template v-if="created.link.hasPassword"> {{ t('sharing.panel.passwordApart') }}</template>
      </p>
      <div class="link-row">
        <input :value="created.url" readonly :aria-label="t('sharing.panel.linkLabel')" />
        <button type="button" class="secondary" @click="onCopy">{{ t('sharing.panel.copy') }}</button>
      </div>
    </div>

    <h3>{{ t('sharing.panel.links') }}</h3>
    <p v-if="listError" class="field-error" role="alert">{{ listError }}</p>
    <p v-else-if="links.length === 0" class="muted">{{ t('sharing.panel.empty') }}</p>
    <ul v-else class="link-list">
      <li v-for="link in links" :key="link.id" class="share-link" data-testid="share-link-item">
        <div class="link-info">
          <span>
            {{ link.label ?? t('sharing.panel.noLabel') }}
            <span class="status" :class="`status-${link.status.toLowerCase()}`">{{ shareStatusLabels[link.status] }}</span>
            <span v-if="link.hasPassword" class="muted"> {{ t('sharing.panel.withPassword') }}</span>
          </span>
          <small class="muted">
            {{ statusLine(link) }} · {{ opensLine(link) }}<template v-if="link.createdByEmail"> {{ t('sharing.panel.createdBy', { createdByEmail: link.createdByEmail }) }}</template>
          </small>
        </div>
        <div class="link-actions">
          <button type="button" class="secondary" :aria-expanded="openJournalId === link.id" @click="onToggleJournal(link)">
            {{ t('sharing.panel.log') }}
          </button>
          <button v-if="link.status === 'Active'" type="button" class="secondary" :disabled="busyId === link.id"
                  @click="onRevoke(link)">{{ t('sharing.panel.revoke') }}</button>
          <button type="button" class="secondary" :disabled="busyId === link.id" @click="onDelete(link)">{{ t('common.actions.delete') }}</button>
        </div>
        <div v-if="openJournalId === link.id" class="journal" data-testid="share-journal">
          <p v-if="journalError" class="field-error" role="alert">{{ journalError }}</p>
          <p v-else-if="journal.length === 0" class="muted">{{ t('sharing.panel.neverOpenedLog') }}</p>
          <table v-else>
            <thead><tr><th scope="col">{{ t('sharing.panel.when') }}</th><th scope="col">{{ t('sharing.panel.what') }}</th><th scope="col">{{ t('sharing.panel.visitor') }}</th><th scope="col">{{ t('sharing.panel.browser') }}</th></tr></thead>
            <tbody>
              <tr v-for="(access, i) in journal" :key="i">
                <td>{{ formatDate(access.at) }}</td>
                <td>{{ accessKindLabels[access.kind] }}<template v-if="access.fileName"> · {{ access.fileName }}</template></td>
                <td><code>{{ access.visitor ?? '?' }}</code></td>
                <td class="agent">{{ access.userAgent ?? '?' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </li>
    </ul>
  </SectionCard>
</template>

<style scoped>
.share-form { display: grid; grid-template-columns: 1fr auto 1fr auto; gap: .75rem; align-items: end; }
.share-form select { width: auto; }
.share-form .field { margin: 0; }
.created { margin: 1rem 0; }
.link-row { display: flex; gap: .5rem; }
.link-row input { flex: 1; min-width: 0; }
h3 { font-size: 1rem; margin: 1rem 0 .5rem; }
.link-list { list-style: none; padding: 0; display: grid; gap: .75rem; }
.share-link { display: grid; grid-template-columns: 1fr auto; gap: .5rem .75rem; align-items: center; }
.link-info { display: grid; overflow-wrap: anywhere; }
.link-actions { display: flex; gap: .5rem; flex-wrap: wrap; }
.status { font-size: .8rem; padding: .05rem .4rem; border-radius: var(--radius); border: 1px solid var(--border); margin-left: .25rem; }
.status-active { border-color: var(--primary); }
.journal { grid-column: 1 / -1; overflow-x: auto; }
.journal table { width: 100%; border-collapse: collapse; font-size: .85rem; }
.journal th, .journal td { text-align: left; padding: .25rem .5rem; border-bottom: 1px solid var(--border); vertical-align: top; }
.agent { max-width: 16rem; overflow-wrap: anywhere; }
@media (max-width: 700px) {
  .share-form { grid-template-columns: 1fr; }
  .share-link { grid-template-columns: 1fr; }
}
</style>