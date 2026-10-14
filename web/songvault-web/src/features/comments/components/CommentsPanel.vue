<script setup lang="ts">
import { computed, reactive, ref, useTemplateRef, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { commentsApi } from '../api/commentsApi'
import { COMMENT_BODY_MAX, type Comment, type CommentTarget } from '../types'
import CommentEntry from './CommentEntry.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import type { Track } from '@/features/player/track'
import LoadingState from '@/shared/components/LoadingState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatTime } from '@/shared/utils/format'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'

/** target : le fichier audio commenté ; track : la même piste pour le lecteur (clic sur un horodatage). */
const props = defineProps<{ target: CommentTarget; track: Track }>()

const auth = useAuthStore()
const bands = useBandStore()
const player = usePlayerStore()
const { track: playing, currentTime } = storeToRefs(player)
const { confirm } = useConfirm()
const toast = useToast()

const threads = ref<Comment[]>([])
const isLoading = ref(false)
const loadError = ref<string | null>(null)
const busy = ref(false)

// ---- Droits : mêmes règles que l'API (ADR 0014), qui reste juge ----
// Tout membre commente et répond ; modifier = l'auteur ; supprimer = l'auteur ou un Owner ;
// résoudre = l'auteur du fil ou un Member.
const isMine = (c: Comment) => c.authorId !== null && c.authorId === auth.user?.id
const canDelete = (c: Comment) => isMine(c) || bands.can('admin')
const canResolve = (c: Comment) => isMine(c) || bands.can('write')

async function load() {
  isLoading.value = true
  loadError.value = null
  try {
    threads.value = await commentsApi.list(props.target)
  } catch (e) {
    loadError.value = getErrorMessage(e)
  } finally {
    isLoading.value = false
  }
}

/** Lance l'action, signale l'échec, puis relit les fils : l'affichage suit toujours le serveur. */
async function act(action: () => Promise<unknown>): Promise<boolean> {
  busy.value = true
  try {
    await action()
    return true
  } catch (e) {
    toast.error(getErrorMessage(e))
    return false
  } finally {
    busy.value = false
    await load()
  }
}

// ================= Nouveau commentaire, réponse en cours =================
const draft = reactive<{ body: string; position: number | null }>({ body: '', position: null })
const replyTo = ref<string | null>(null)       // fil dont le formulaire de réponse est ouvert
const replyBody = ref('')
const bodyInput = useTemplateRef<HTMLTextAreaElement>('body-input')
/** La position ne se prend que sur ce fichier, quand il est dans le lecteur. */
const isPlayingThisFile = computed(() => playing.value?.src === props.track.src)

// Autre fichier : autres fils, et le brouillon ne le concerne plus
watch(() => props.target.fileId, () => {
  draft.body = ''
  draft.position = null
  replyTo.value = null
  void load()
}, { immediate: true })

function capturePosition() {
  draft.position = currentTime.value
  bodyInput.value?.focus()
}

async function onCreate() {
  const body = draft.body.trim()
  if (!body) return
  const positionMs = draft.position === null ? null : Math.round(draft.position * 1000)
  if (await act(() => commentsApi.create(props.target, body, positionMs))) {
    draft.body = ''
    draft.position = null
  }
}

// ================= Fil : écoute, réponse, résolution =================
function onListen(thread: Comment) {
  if (thread.positionMs !== null) void player.playAt(props.track, thread.positionMs / 1000)
}

function openReply(thread: Comment) {
  replyTo.value = thread.id
  replyBody.value = ''
}

async function onReply(thread: Comment) {
  const body = replyBody.value.trim()
  if (!body) return
  if (await act(() => commentsApi.reply(props.target, thread.id, body))) replyTo.value = null
}

function onToggleResolved(thread: Comment) {
  void act(() => thread.resolvedAt
    ? commentsApi.reopen(props.target, thread.id)
    : commentsApi.resolve(props.target, thread.id))
}

// ================= Commentaire : modification, suppression =================
function onEdit(comment: Comment, body: string) {
  void act(() => commentsApi.edit(props.target, comment.id, body))
}

async function onDelete(comment: Comment) {
  const replies = comment.replies.length
  const confirmed = await confirm({
    title: 'Supprimer ce commentaire ?',
    message: replies > 0
      ? `Le commentaire et ${replies === 1 ? 'sa réponse seront supprimés' : `ses ${replies} réponses seront supprimés`}.`
      : 'Le commentaire sera supprimé définitivement.',
    confirmLabel: 'Supprimer',
    danger: true,
  })
  if (confirmed) void act(() => commentsApi.remove(props.target, comment.id))
}
</script>

<template>
  <form class="new-comment" novalidate @submit.prevent="onCreate">
    <label for="comment-body">Nouveau commentaire</label>
     <textarea id="comment-body" ref="body-input" v-model="draft.body" rows="2" :maxlength="COMMENT_BODY_MAX"
              placeholder="ex. la basse décale au refrain" aria-describedby="comment-mention-help" />
    <small id="comment-mention-help" class="muted">
      Pour prévenir un membre par e-mail, écrivez @ suivi de son adresse (ex. @ana@exemple.fr).
    </small>
    <div class="new-comment-bar">
      <button v-if="draft.position === null" type="button" class="secondary" :disabled="!isPlayingThisFile"
              data-testid="capture-position" @click="capturePosition">
        Commenter à {{ formatTime(isPlayingThisFile ? currentTime : 0) }}
      </button>
      <span v-else class="position-chip" data-testid="draft-position">
        À {{ formatTime(draft.position) }}
        <button type="button" class="secondary" aria-label="Retirer la position" @click="draft.position = null">✕</button>
      </span>
      <small v-if="!isPlayingThisFile" class="muted">Écoutez ce fichier pour commenter un instant précis.</small>
      <button type="submit" class="publish" :disabled="busy || !draft.body.trim()">Publier</button>
    </div>
  </form>

  <p v-if="loadError" class="field-error" role="alert">{{ loadError }}</p>
  <LoadingState v-else-if="isLoading && threads.length === 0" />
  <p v-else-if="threads.length === 0" class="muted">Aucun commentaire sur ce fichier.</p>
  <ol v-else class="threads">
    <li v-for="thread in threads" :key="thread.id" class="thread" :class="{ resolved: thread.resolvedAt }"
        data-testid="comment-thread">
      <div class="thread-head">
        <button v-if="thread.positionMs !== null" type="button" class="timestamp"
                :aria-label="`Écouter à partir de ${formatTime(thread.positionMs / 1000)}`" @click="onListen(thread)">
          ▶ {{ formatTime(thread.positionMs / 1000) }}
        </button>
        <span v-else class="muted">Général</span>
        <span v-if="thread.resolvedAt" class="resolved-badge">
          Résolu<template v-if="thread.resolvedByEmail"> par {{ thread.resolvedByEmail }}</template>
        </span>
      </div>

      <CommentEntry :comment="thread" :can-edit="isMine(thread)" :can-delete="canDelete(thread)" :busy="busy"
                    @save="onEdit(thread, $event)" @remove="onDelete(thread)">
        <template #actions>
          <button type="button" class="secondary" :disabled="busy" @click="openReply(thread)">Répondre</button>
          <button v-if="canResolve(thread)" type="button" class="secondary" :disabled="busy"
                  @click="onToggleResolved(thread)">
            {{ thread.resolvedAt ? 'Rouvrir' : 'Résoudre' }}
          </button>
        </template>
      </CommentEntry>

      <ul v-if="thread.replies.length > 0" class="replies">
        <li v-for="reply in thread.replies" :key="reply.id">
          <CommentEntry :comment="reply" :can-edit="isMine(reply)" :can-delete="canDelete(reply)" :busy="busy"
                        @save="onEdit(reply, $event)" @remove="onDelete(reply)" />
        </li>
      </ul>

      <form v-if="replyTo === thread.id" class="reply-form" novalidate @submit.prevent="onReply(thread)">
        <textarea v-model="replyBody" rows="2" :maxlength="COMMENT_BODY_MAX" aria-label="Votre réponse" />
        <div class="reply-actions">
          <button type="button" class="secondary" @click="replyTo = null">Annuler</button>
          <button type="submit" :disabled="busy || !replyBody.trim()">Envoyer la réponse</button>
        </div>
      </form>
    </li>
  </ol>
</template>

<style scoped>
.new-comment { display: grid; gap: .5rem; margin-bottom: 1rem; }
.new-comment-bar { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
.publish { margin-left: auto; }
.position-chip { display: inline-flex; gap: .5rem; align-items: center; font-variant-numeric: tabular-nums; }
.position-chip button { padding: 0 .5rem; }
.threads { list-style: none; padding: 0; margin: 0; display: grid; gap: 1rem; }
.thread { display: grid; gap: .5rem; padding-top: 1rem; border-top: 1px solid var(--border); }
.thread.resolved { opacity: .7; }
.thread-head { display: flex; gap: .75rem; align-items: center; }
.timestamp { padding: .1rem .6rem; font-variant-numeric: tabular-nums; }
.resolved-badge { font-size: .85rem; color: var(--muted); }
.replies { list-style: none; margin: 0; padding-left: 1.25rem; border-left: 2px solid var(--border); display: grid; gap: .75rem; }
.reply-form { display: grid; gap: .5rem; padding-left: 1.25rem; }
.reply-actions { display: flex; gap: .5rem; justify-content: flex-end; }
</style>