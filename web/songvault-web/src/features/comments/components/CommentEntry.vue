<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { COMMENT_BODY_MAX, type Comment } from '../types'
import { formatDate } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

const props = defineProps<{ comment: Comment; canEdit: boolean; canDelete: boolean; busy: boolean }>()
const emit = defineEmits<{ save: [body: string]; remove: [] }>()

const auth = useAuthStore()
const author = computed(() => {
  if (props.comment.authorId !== null && props.comment.authorId === auth.user?.id) return t('comments.entry.you')
  return props.comment.authorEmail ?? t('comments.entry.deletedAccount')
})

// Modification sur place : le texte d'origine reste affiché tant qu'on n'a pas enregistré
const isEditing = ref(false)
const draft = ref('')

function startEdit() {
  draft.value = props.comment.body
  isEditing.value = true
}

function onSave() {
  const body = draft.value.trim()
  if (!body) return
  emit('save', body)
  isEditing.value = false
}
</script>

<template>
  <article class="comment" data-testid="comment">
    <header class="comment-meta">
      <strong>{{ author }}</strong>
      <small class="muted">
        {{ formatDate(comment.createdAt) }}<template v-if="comment.editedAt"> {{ t('comments.entry.edited') }}</template>
      </small>
    </header>

    <form v-if="isEditing" class="comment-edit" @submit.prevent="onSave">
      <textarea v-model="draft" rows="2" :maxlength="COMMENT_BODY_MAX" :aria-label="t('comments.entry.editLabel')" />
      <div class="comment-actions">
        <button type="button" class="secondary" @click="isEditing = false">{{ t('common.actions.cancel') }}</button>
        <button type="submit" :disabled="busy || !draft.trim()">{{ t('common.actions.save') }}</button>
      </div>
    </form>
    <p v-else class="comment-body">{{ comment.body }}</p>

    <div v-if="!isEditing" class="comment-actions">
      <slot name="actions" />
      <button v-if="canEdit" type="button" class="secondary" :disabled="busy" @click="startEdit">{{ t('comments.entry.edit') }}</button>
      <button v-if="canDelete" type="button" class="secondary" :disabled="busy" @click="$emit('remove')">{{ t('common.actions.delete') }}</button>
    </div>
  </article>
</template>

<style scoped>
.comment { display: grid; gap: .25rem; }
.comment-meta { display: flex; gap: .5rem; align-items: baseline; flex-wrap: wrap; overflow-wrap: anywhere; }
.comment-body { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }
.comment-edit { display: grid; gap: .5rem; }
.comment-actions { display: flex; gap: .5rem; flex-wrap: wrap; }
.comment-actions :deep(button) { padding: .25rem .6rem; font-size: .9rem; }
</style>