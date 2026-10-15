import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { PREVIEW_POLL_MS, usePreviewPolling } from '../composables/usePreviewPolling'
import type { SongFile } from '../types'

const file = (id: string, preview: SongFile['preview']): SongFile => ({
  id, originalFileName: `${id}.wav`, contentType: 'audio/wav', sizeBytes: 1,
  fileType: 'Audio', uploadedAt: '2026-10-15T10:00:00Z', preview,
})

function mountPolling(files: SongFile[], fetchFiles: () => Promise<SongFile[]>) {
  const list = ref(files)
  const wrapper = mount(defineComponent({
    setup() {
      usePreviewPolling(list, fetchFiles)
      return () => h('div')
    },
  }))
  return { list, wrapper }
}

describe('usePreviewPolling', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it("relit l'état toutes les 5 secondes tant qu'un aperçu est en préparation", async () => {
    const fetchFiles = vi.fn()
      .mockResolvedValueOnce([file('a', 'Pending')])
      .mockResolvedValueOnce([file('a', 'Ready')])
    const { list } = mountPolling([file('a', 'Pending'), file('pdf', null)], fetchFiles)

    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS)
    expect(list.value[0]!.preview).toBe('Pending')
    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS)
    expect(list.value[0]!.preview).toBe('Ready')
    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS * 3)

    expect(fetchFiles).toHaveBeenCalledTimes(2)              // plus rien en préparation : on s'arrête
  })

  it("ne fait rien sans aperçu en préparation, et reprend après une relance", async () => {
    const fetchFiles = vi.fn().mockResolvedValue([file('a', 'Ready')])
    const { list } = mountPolling([file('a', 'Failed')], fetchFiles)

    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS * 2)
    expect(fetchFiles).not.toHaveBeenCalled()

    list.value[0]!.preview = 'Pending'
    await flushPromises()
    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS)
    expect(list.value[0]!.preview).toBe('Ready')
  })

  it("s'arrête quand la page est quittée", async () => {
    const fetchFiles = vi.fn().mockResolvedValue([file('a', 'Pending')])
    const { wrapper } = mountPolling([file('a', 'Pending')], fetchFiles)

    wrapper.unmount()
    await vi.advanceTimersByTimeAsync(PREVIEW_POLL_MS * 2)

    expect(fetchFiles).not.toHaveBeenCalled()
  })
})