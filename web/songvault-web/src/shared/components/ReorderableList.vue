<script setup lang="ts" generic="T">
import { nextTick, ref } from 'vue'
import { t } from '@/shared/i18n'

/**
 * Liste ordonnée qu'on réordonne à la souris (glisser-déposer) ou au clavier (boutons Monter et Descendre).
 * Ne modifie pas items : émet « move » avec la nouvelle liste, le parent décide de l'enregistrer.
 * Sert aux pistes d'une sortie (#10), puis aux setlists (#21).
 */
const props = defineProps<{
  items: readonly T[]
  itemKey: (item: T) => string
  /** Nom lu par les lecteurs d'écran : « Monter Aube », « Aube déplacé en position 2 sur 3 » */
  itemLabel: (item: T) => string
  /** Liste figée : ni boutons ni glisser-déposer (lecture seule, enregistrement en cours) */
  disabled?: boolean
  /** Nom de la liste pour les lecteurs d'écran */
  label: string
}>()

const emit = defineEmits<{ move: [items: T[], from: number, to: number] }>()

const announcement = ref('')
const dragIndex = ref<number | null>(null)
const overIndex = ref<number | null>(null)
const listEl = ref<HTMLOListElement | null>(null)

function moved(from: number, to: number): T[] {
  const next = [...props.items]
  const [item] = next.splice(from, 1)
  next.splice(to, 0, item as T)
  return next
}

function move(from: number, to: number, focusButton?: 'up' | 'down') {
  if (props.disabled || from === to || to < 0 || to >= props.items.length) return
  const item = props.items[from] as T
  emit('move', moved(from, to), from, to)
  announcement.value = t('shared.reorder.moved', { item: props.itemLabel(item), to: to + 1, count: props.items.length })

  // Le focus suit l'élément déplacé ; en haut ou en bas de la liste, le bouton grisé passe la main à l'autre
  if (!focusButton) return
  void nextTick(() => {
    const key = props.itemKey(item)
    const row = [...(listEl.value?.querySelectorAll<HTMLElement>('[data-key]') ?? [])].find((el) => el.dataset.key === key)
    const wanted = row?.querySelector<HTMLButtonElement>(`[data-move="${focusButton}"]:not(:disabled)`)
    ;(wanted ?? row?.querySelector<HTMLButtonElement>('[data-move]:not(:disabled)'))?.focus()
  })
}

function onDragStart(index: number, event: DragEvent) {
  if (props.disabled) return
  dragIndex.value = index
  event.dataTransfer?.setData('text/plain', String(index))     // Firefox ne lance pas le glisser sans données
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onDragOver(index: number, event: DragEvent) {
  if (dragIndex.value === null) return
  event.preventDefault()                                        // autorise le dépôt ici
  overIndex.value = index
}

function onDrop(index: number) {
  if (dragIndex.value !== null) move(dragIndex.value, index)
  onDragEnd()
}

function onDragEnd() {
  dragIndex.value = null
  overIndex.value = null
}
</script>

<template>
  <div>
    <ol ref="listEl" class="reorderable" :aria-label="label">
      <li
        v-for="(item, index) in items"
        :key="itemKey(item)"
        :data-key="itemKey(item)"
        class="reorderable-item"
        :class="{ dragging: dragIndex === index, over: overIndex === index && dragIndex !== index }"
        :draggable="!disabled"
        data-testid="reorderable-item"
        @dragstart="onDragStart(index, $event)"
        @dragover="onDragOver(index, $event)"
        @drop.prevent="onDrop(index)"
        @dragend="onDragEnd"
      >
        <span v-if="!disabled" class="handle" aria-hidden="true" :title="t('shared.reorder.drag')">⠿</span>
        <div class="content"><slot :item="item" :index="index" /></div>
        <span v-if="!disabled" class="moves">
          <button type="button" class="secondary" data-move="up" :disabled="index === 0"
                  :aria-label="t('shared.reorder.up', { item: itemLabel(item) })" @click="move(index, index - 1, 'up')">↑</button>
          <button type="button" class="secondary" data-move="down" :disabled="index === items.length - 1"
                  :aria-label="t('shared.reorder.down', { item: itemLabel(item) })" @click="move(index, index + 1, 'down')">↓</button>
        </span>
      </li>
    </ol>
    <p class="visually-hidden" role="status" aria-live="polite">{{ announcement }}</p>
  </div>
</template>

<style scoped>
.reorderable { list-style: none; padding: 0; margin: 0; display: grid; gap: .4rem; }
.reorderable-item {
  display: grid; grid-template-columns: auto 1fr auto; gap: .6rem; align-items: center;
  padding: .5rem .75rem; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface);
}
.reorderable-item[draggable='true'] { cursor: grab; }
.reorderable-item.dragging { opacity: .5; }
.reorderable-item.over { outline: 2px dashed currentColor; outline-offset: 2px; }
.handle { color: var(--muted); user-select: none; }
.content { min-width: 0; }
.moves { display: flex; gap: .25rem; }
.moves button { padding: .2rem .55rem; }
</style>