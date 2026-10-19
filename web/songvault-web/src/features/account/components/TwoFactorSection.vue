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
  if (!password.value) { error.value = 'Saisissez votre mot de passe.'; return }
  const ok = await attempt(async () => { recoveryCodes.value = await accountApi.regenerateRecoveryCodes(password.value) })
  if (!ok) return
  password.value = ''
  await load()
  emit('changed')
  await nextTick()
  codesList.value?.focus()
}

async function disable() {
  if (!password.value) { error.value = 'Saisissez votre mot de passe.'; return }
  const confirmed = await confirm({
    title: 'Désactiver la double authentification ?',
    message: 'Votre mot de passe suffira de nouveau pour vous connecter. Vos codes de secours ne serviront plus.',
    confirmLabel: 'Désactiver',
    danger: true,
  })
  if (!confirmed) return
  const ok = await attempt(() => accountApi.disableTwoFactor(password.value))
  if (!ok) return
  password.value = ''
  recoveryCodes.value = null
  await load()
  emit('changed')
  toast.success('Double authentification désactivée.')
}

async function forgetBrowser() {
  if (await attempt(() => accountApi.forgetBrowser())) {
    await load()
    toast.success('Le code sera redemandé à la prochaine connexion sur ce navigateur.')
  }
}

async function copyCodes() {
  try {
    await navigator.clipboard.writeText((recoveryCodes.value ?? []).join('\n'))
    toast.success('Codes copiés.')
  } catch {
    error.value = 'Copie impossible : recopiez les codes à la main.'
  }
}
</script>

<template>
  <SectionCard title="Double authentification">
    <ErrorState v-if="loadError" :message="getErrorMessage(loadError)" @retry="load" />
    <LoadingState v-else-if="!status" />

    <!-- Codes de secours tout juste créés : montrés une seule fois -->
    <div v-else-if="recoveryCodes" class="codes">
      <p><strong>Gardez ces codes de secours en lieu sûr.</strong>
        Chacun permet une connexion si vous n’avez plus votre téléphone. Ils ne seront plus affichés.</p>
      <ol ref="codesList" class="code-list" tabindex="-1" aria-label="Codes de secours" data-testid="recovery-codes">
        <li v-for="c in recoveryCodes" :key="c"><code>{{ c }}</code></li>
      </ol>
      <div class="actions">
        <button type="button" class="secondary" @click="copyCodes">Copier les codes</button>
        <button type="button" @click="recoveryCodes = null">J’ai noté mes codes</button>
      </div>
    </div>

    <!-- Activation en cours : la clé, puis un premier code -->
    <form v-else-if="setup" class="narrow" novalidate @submit.prevent="enable">
      <ol class="steps">
        <li>Ouvrez votre application d’authentification (Google Authenticator, Microsoft Authenticator, Aegis, 1Password…).</li>
        <li>
          Ajoutez un compte avec cette clé :
          <code class="key" data-testid="shared-key">{{ setup.sharedKey }}</code>
          <a :href="setup.authenticatorUri">Sur ce téléphone : ouvrir dans l’application</a>
        </li>
        <li>Saisissez le code à 6 chiffres qu’elle affiche.</li>
      </ol>
      <div class="field">
        <label for="two-factor-code">Code de l’application</label>
        <input id="two-factor-code" ref="codeInput" v-model="code" type="text" inputmode="numeric"
               autocomplete="one-time-code" maxlength="7" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <div class="actions">
        <button type="submit" :disabled="!canEnable">{{ isBusy ? 'Activation…' : 'Activer' }}</button>
        <button type="button" class="secondary" @click="cancelSetup">Annuler</button>
      </div>
    </form>

    <!-- Inactive -->
    <div v-else-if="!status.enabled">
      <p class="muted">
        Désactivée. Activée, la connexion demande en plus de votre mot de passe un code à 6 chiffres
        donné par une application sur votre téléphone.
      </p>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="button" :disabled="isBusy" @click="startSetup">Activer la double authentification</button>
    </div>

    <!-- Active -->
    <form v-else class="narrow" novalidate @submit.prevent>
      <p>
        <strong>Activée.</strong>
        {{ status.recoveryCodesLeft }} code{{ status.recoveryCodesLeft > 1 ? 's' : '' }} de secours
        restant{{ status.recoveryCodesLeft > 1 ? 's' : '' }}.
      </p>
      <p v-if="status.browserRemembered" class="muted">
        Ce navigateur ne vous demande pas de code pendant 14 jours après une connexion avec code.
        <button type="button" class="link" :disabled="isBusy" @click="forgetBrowser">Redemander le code ici</button>
      </p>
      <div class="field">
        <label for="two-factor-password">Mot de passe</label>
        <input id="two-factor-password" v-model="password" type="password" autocomplete="current-password" required />
        <small class="muted">Redemandé pour créer de nouveaux codes de secours ou désactiver.</small>
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <div class="actions">
        <button type="button" class="secondary" :disabled="isBusy" @click="regenerate">Nouveaux codes de secours</button>
        <button type="button" class="danger" :disabled="isBusy" @click="disable">Désactiver</button>
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