<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { accountApi } from '../api/accountApi'
import type { TwoFactorSetup, TwoFactorStatus } from '../types'
import { t } from '@/shared/i18n'

const emit = defineEmits<{ changed: [] }>()
const toast = useToast()
const { confirm } = useConfirm()

const status = ref<TwoFactorStatus | null>(null)
const loadError = ref<unknown>(null)
const setup = ref<TwoFactorSetup | null>(null)
const recoveryCodes = ref<string[] | null>(null)
const code = ref('')
const password = ref('')
const error = ref<string | null>(null)
const isBusy = ref(false)
const codeInput = ref<HTMLInputElement | null>(null)
const codesList = ref<HTMLElement | null>(null)

const canEnable = computed(() => /^\d{6}$/.test(code.value.replace(/[\s-]/g, '')) && !isBusy.value)

async function load() {
  loadError.value = null
  try {
    status.value = await accountApi.twoFactorStatus()
  } catch (e) {
    loadError.value = e
  }
}
onMounted(load)

/** Exécute l'action, affiche son erreur sous le formulaire ; renvoie false si elle a échoué. */
async function attempt(action: () => Promise<void>): Promise<boolean> {
  error.value = null
  isBusy.value = true
  try {
    await action()
    return true
  } catch (e) {
    error.value = getErrorMessage(e)
    return false
  } finally {
    isBusy.value = false
  }
}

async function startSetup() {
  if (await attempt(async () => { setup.value = await accountApi.setupTwoFactor() })) {
    await nextTick()
    codeInput.value?.focus()
  }
}

async function enable() {
  if (!canEnable.value) return
  const ok = await attempt(async () => {
    recoveryCodes.value = await accountApi.enableTwoFactor(code.value.replace(/[\s-]/g, ''))
  })
  if (!ok) return
  setup.value = null
  code.value = ''
  await load()
  emit('changed')
  await nextTick()
  codesList.value?.focus()
}

function cancelSetup() {
  setup.value = null
  code.value = ''
  error.value = null
}

async function regenerate() {
  if (!password.value) { error.value = t('account.twoFactor.passwordRequired'); return }
  const ok = await attempt(async () => { recoveryCodes.value = await accountApi.regenerateRecoveryCodes(password.value) })
  if (!ok) return
  password.value = ''
  await load()
  emit('changed')
  await nextTick()
  codesList.value?.focus()
}

async function disable() {
  if (!password.value) { error.value = t('account.twoFactor.passwordRequired'); return }
  const confirmed = await confirm({
    title: t('account.twoFactor.disableTitle'),
    message: t('account.twoFactor.disableMessage'),
    confirmLabel: t('account.twoFactor.disable'),
    danger: true,
  })
  if (!confirmed) return
  const ok = await attempt(() => accountApi.disableTwoFactor(password.value))
  if (!ok) return
  password.value = ''
  recoveryCodes.value = null
  await load()
  emit('changed')
  toast.success(t('account.twoFactor.disabled'))
}

async function forgetBrowser() {
  if (await attempt(() => accountApi.forgetBrowser())) {
    await load()
    toast.success(t('account.twoFactor.forgotten'))
  }
}

async function copyCodes() {
  try {
    await navigator.clipboard.writeText((recoveryCodes.value ?? []).join('\n'))
    toast.success(t('account.twoFactor.copied'))
  } catch {
    error.value = t('account.twoFactor.copyFailed')
  }
}
</script>

<template>
  <SectionCard :title="t('account.twoFactor.title')">
    <ErrorState v-if="loadError" :message="getErrorMessage(loadError)" @retry="load" />
    <LoadingState v-else-if="!status" />

    <!-- Codes de secours tout juste créés : montrés une seule fois -->
    <div v-else-if="recoveryCodes" class="codes">
      <p><strong>{{ t('account.twoFactor.keepCodes') }}</strong>
        {{ t('account.twoFactor.codesHelp') }}</p>
      <ol ref="codesList" class="code-list" tabindex="-1" :aria-label="t('account.twoFactor.codesLabel')" data-testid="recovery-codes">
        <li v-for="c in recoveryCodes" :key="c"><code>{{ c }}</code></li>
      </ol>
      <div class="actions">
        <button type="button" class="secondary" @click="copyCodes">{{ t('account.twoFactor.copyCodes') }}</button>
        <button type="button" @click="recoveryCodes = null">{{ t('account.twoFactor.codesNoted') }}</button>
      </div>
    </div>

    <!-- Activation en cours : la clé, puis un premier code -->
    <form v-else-if="setup" class="narrow" novalidate @submit.prevent="enable">
      <ol class="steps">
        <li>{{ t('account.twoFactor.step1') }}</li>
        <li>
          {{ t('account.twoFactor.step2') }}
          <code class="key" data-testid="shared-key">{{ setup.sharedKey }}</code>
          <a :href="setup.authenticatorUri">{{ t('account.twoFactor.openInApp') }}</a>
        </li>
        <li>{{ t('account.twoFactor.step3') }}</li>
      </ol>
      <div class="field">
        <label for="two-factor-code">{{ t('account.twoFactor.appCode') }}</label>
        <input id="two-factor-code" ref="codeInput" v-model="code" type="text" inputmode="numeric"
               autocomplete="one-time-code" maxlength="7" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <div class="actions">
        <button type="submit" :disabled="!canEnable">{{ isBusy ? t('account.twoFactor.enabling') : t('account.twoFactor.enable') }}</button>
        <button type="button" class="secondary" @click="cancelSetup">{{ t('common.actions.cancel') }}</button>
      </div>
    </form>

    <!-- Inactive -->
    <div v-else-if="!status.enabled">
      <p class="muted">
        {{ t('account.twoFactor.offHelp') }}
      </p>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="button" :disabled="isBusy" @click="startSetup">{{ t('account.twoFactor.enableButton') }}</button>
    </div>

    <!-- Active -->
    <form v-else class="narrow" novalidate @submit.prevent>
      <p>
        <strong>{{ t('account.twoFactor.on') }}</strong>
        {{ t('account.twoFactor.codesLeft', { count: status.recoveryCodesLeft }) }}
      </p>
      <p v-if="status.browserRemembered" class="muted">
        {{ t('account.twoFactor.rememberHelp') }}
        <button type="button" class="link" :disabled="isBusy" @click="forgetBrowser">{{ t('account.twoFactor.forget') }}</button>
      </p>
      <div class="field">
        <label for="two-factor-password">{{ t('account.twoFactor.password') }}</label>
        <input id="two-factor-password" v-model="password" type="password" autocomplete="current-password" required />
        <small class="muted">{{ t('account.twoFactor.passwordHelp') }}</small>
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <div class="actions">
        <button type="button" class="secondary" :disabled="isBusy" @click="regenerate">{{ t('account.twoFactor.newCodes') }}</button>
        <button type="button" class="danger" :disabled="isBusy" @click="disable">{{ t('account.twoFactor.disable') }}</button>
      </div>
    </form>
  </SectionCard>
</template>

<style scoped>
.narrow { max-width: 30rem; }
.steps { padding-left: 1.2rem; display: grid; gap: .5rem; }
.key { display: block; margin: .4rem 0; font-size: 1.1rem; letter-spacing: .05em; overflow-wrap: anywhere; }
.code-list { columns: 2; padding-left: 1.5rem; margin: .5rem 0 1rem; }
.code-list code { font-size: 1rem; }
.actions { display: flex; flex-wrap: wrap; gap: .5rem; }
.link { background: none; border: none; padding: 0; color: var(--primary); text-decoration: underline; cursor: pointer; }
</style>