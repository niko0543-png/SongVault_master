<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useBandStore } from '../stores/bandStore'
import { membersApi } from '../api/membersApi'
import type { BandMember, BandRole } from '../types'
import { useAuthStore } from '@/features/auth/stores/authStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import InvitationsPanel from '@/features/invitations/components/InvitationsPanel.vue'

const bands = useBandStore()
const { active } = storeToRefs(bands)
const auth = useAuthStore()
const router = useRouter()
const { confirm } = useConfirm()
const toast = useToast()

const roleLabels: Record<BandRole, string> = { Owner: 'Propriétaire', Member: 'Membre', Guest: 'Invité' }
const ROLES: BandRole[] = ['Owner', 'Member', 'Guest']

const members = ref<BandMember[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)
const actionError = ref<string | null>(null)
const busyId = ref<string | null>(null)
const isAdmin = computed(() => bands.can('admin'))
const myId = computed(() => auth.user?.id)

async function load() {
  isLoading.value = true
  error.value = null
  try {
    members.value = await membersApi.list()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
// Recharge aussi quand on change de groupe sans quitter la page (même composant, autre bandId)
watch(() => bands.activeId, (id) => { if (id) void load() }, { immediate: true })

/** Lance une action sur un membre ; l'erreur de l'API (403, 409) s'affiche sous le titre. */
async function run(userId: string, action: () => Promise<void>): Promise<boolean> {
  busyId.value = userId
  actionError.value = null
  try {
    await action()
    return true
  } catch (e) {
    actionError.value = getErrorMessage(e)
    return false
  } finally {
    busyId.value = null
  }
}

async function onChangeRole(member: BandMember, event: Event) {
  const select = event.target as HTMLSelectElement
  const role = select.value as BandRole
  if (await run(member.userId, () => membersApi.changeRole(member.userId, role))) {
    members.value = members.value.map((m) => (m.userId === member.userId ? { ...m, role } : m))
    if (member.userId === myId.value && active.value) bands.setRole(active.value.id, role)
  } else {
    select.value = member.role            // refusé : le sélecteur revient au rôle réel
  }
}

async function onRemove(member: BandMember) {
  const confirmed = await confirm({
    title: 'Retirer ce membre ?',
    message: `${member.email} n'aura plus accès aux morceaux du groupe.`,
    confirmLabel: 'Retirer',
    danger: true,
  })
  if (!confirmed) return
  if (await run(member.userId, () => membersApi.remove(member.userId))) {
    members.value = members.value.filter((m) => m.userId !== member.userId)
    toast.success(`${member.email} a été retiré du groupe.`)
  }
}

async function onLeave() {
  const band = active.value
  if (!band) return
  const confirmed = await confirm({
    title: 'Quitter ce groupe ?',
    message: `Vous n'aurez plus accès aux morceaux de « ${band.name} ».`,
    confirmLabel: 'Quitter',
    danger: true,
  })
  if (!confirmed) return
  if (await run(myId.value ?? '', membersApi.leave)) {
    bands.remove(band.id)
    toast.success(`Vous avez quitté « ${band.name} ».`)
    await router.push({ name: 'home' })
  }
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Membres</h1>
      <button type="button" class="secondary" :disabled="busyId !== null" @click="onLeave">Quitter le groupe</button>
    </header>
    <p v-if="actionError" class="field-error" role="alert">{{ actionError }}</p>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />

    <ul v-else class="member-list">
      <li v-for="member in members" :key="member.userId" class="card member" data-testid="member-item">
        <div class="member-info">
          <span>{{ member.email }}<template v-if="member.userId === myId"> (vous)</template></span>
          <small class="muted">Arrivé le {{ formatDate(member.joinedAt) }}</small>
        </div>
        <select v-if="isAdmin" :value="member.role" :disabled="busyId === member.userId"
                :aria-label="`Rôle de ${member.email}`" @change="onChangeRole(member, $event)">
          <option v-for="role in ROLES" :key="role" :value="role">{{ roleLabels[role] }}</option>
        </select>
        <span v-else>{{ roleLabels[member.role] }}</span>
        <button v-if="isAdmin && member.userId !== myId" type="button" class="secondary"
                :disabled="busyId === member.userId" @click="onRemove(member)">Retirer</button>
      </li>
    </ul>
    <InvitationsPanel v-if="isAdmin" />
  </section>
</template>

<style scoped>
.member-list { list-style: none; padding: 0; display: grid; gap: .5rem; }
.member { display: grid; grid-template-columns: 1fr auto auto; gap: .75rem; align-items: center; }
.member-info { display: grid; overflow-wrap: anywhere; }
.member select { width: auto; }
</style>