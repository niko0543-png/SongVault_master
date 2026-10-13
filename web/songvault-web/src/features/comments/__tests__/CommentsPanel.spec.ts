import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import CommentsPanel from '../components/CommentsPanel.vue'
import { commentsApi } from '../api/commentsApi'
import type { Comment, CommentTarget } from '../types'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import type { Track } from '@/features/player/track'

vi.mock('../api/commentsApi', () => ({
  commentsApi: {
    list: vi.fn(), create: vi.fn(), reply: vi.fn(), edit: vi.fn(), remove: vi.fn(), resolve: vi.fn(), reopen: vi.fn(),
  },
}))

const target: CommentTarget = { songId: 's1', versionId: 'v1', fileId: 'f1' }
const track: Track = { id: 'f1', title: 'Nocturne — v1', subtitle: 'maquette.mp3', src: '/api/f1' }

function comment(overrides: Partial<Comment>): Comment {
  return {
    id: 'c1', parentId: null, authorId: 'u-owner', authorEmail: 'owner@test.local', positionMs: 102_000,
    body: 'La basse décale', createdAt: '2026-10-14T10:00:00Z', editedAt: null,
    resolvedAt: null, resolvedByEmail: null, replies: [], ...overrides,
  }
}

const threads: Comment[] = [
  comment({ id: 'c0', positionMs: null, body: 'Bon mix', authorId: 'u-guest', authorEmail: 'guest@test.local' }),
  comment({
    id: 'c1',
    replies: [comment({ id: 'r1', parentId: 'c1', positionMs: null, body: 'Corrigé en v2', authorId: null, authorEmail: null })],
  }),
]

/** Monté par un Guest (u-guest) ; « playing » : piste chargée dans le lecteur, et sa position. */
async function mountPanel(playing: { track: Track | null; currentTime: number } = { track: null, currentTime: 0 }) {
  const wrapper = mount(CommentsPanel, {
    props: { target, track },
    global: {
      plugins: [createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,
        initialState: {
          auth: { user: { id: 'u-guest', email: 'guest@test.local' }, isLoaded: true },
          band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Guest' }], activeId: 'b1', isLoaded: true },
          player: playing,
        },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

describe('CommentsPanel', () => {
  beforeEach(() => {
    vi.mocked(commentsApi.list).mockReset().mockResolvedValue(threads)
    vi.mocked(commentsApi.create).mockReset().mockResolvedValue(comment({}))
  })

  it('affiche les fils, leurs horodatages et leurs réponses', async () => {
    const w = await mountPanel()

    const items = w.findAll('[data-testid="comment-thread"]')
    expect(items).toHaveLength(2)
    expect(items[0]!.text()).toContain('Général')
    expect(items[0]!.text()).toContain('Vous')                       // le Guest est l'auteur
    expect(items[1]!.text()).toContain('1:42')
    expect(items[1]!.text()).toContain('Corrigé en v2')
    expect(items[1]!.text()).toContain('Compte supprimé')
  })

  it("le clic sur l'horodatage lance la lecture à cette position", async () => {
    const w = await mountPanel()

    await w.get('button[aria-label="Écouter à partir de 1:42"]').trigger('click')

    expect(usePlayerStore().playAt).toHaveBeenCalledWith(track, 102)
  })

  it('« Commenter à » prend la position du lecteur', async () => {
    const w = await mountPanel({ track, currentTime: 102.4 })

    await w.get('[data-testid="capture-position"]').trigger('click')
    await w.get('textarea#comment-body').setValue('La basse décale')
    await w.get('form.new-comment').trigger('submit')
    await flushPromises()

    expect(commentsApi.create).toHaveBeenCalledWith(target, 'La basse décale', 102_400)
    expect(commentsApi.list).toHaveBeenCalledTimes(2)                // relu après l'envoi
  })

  it("sans ce fichier dans le lecteur, le commentaire est général", async () => {
    const w = await mountPanel()

    expect(w.get('[data-testid="capture-position"]').attributes('disabled')).toBeDefined()
    await w.get('textarea#comment-body').setValue('Bon mix')
    await w.get('form.new-comment').trigger('submit')
    await flushPromises()

    expect(commentsApi.create).toHaveBeenCalledWith(target, 'Bon mix', null)
  })

  it("un Guest modifie et résout seulement ce qu'il a écrit", async () => {
    const w = await mountPanel()

    const [own, other] = w.findAll('[data-testid="comment-thread"]')
    const buttons = (item: typeof own) => item!.findAll('button').map((b) => b.text())
    expect(buttons(own)).toEqual(expect.arrayContaining(['Répondre', 'Résoudre', 'Modifier', 'Supprimer']))
    expect(buttons(other)).toContain('Répondre')
    expect(buttons(other)).not.toContain('Résoudre')
    expect(buttons(other)).not.toContain('Modifier')
    expect(buttons(other)).not.toContain('Supprimer')
  })
})