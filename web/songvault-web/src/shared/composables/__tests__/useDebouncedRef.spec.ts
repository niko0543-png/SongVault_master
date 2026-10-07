import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick, ref } from 'vue'
import { useDebouncedRef } from '../useDebouncedRef'

describe('useDebouncedRef', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it("ne met à jour qu'après le délai, avec la dernière valeur", async () => {
    const source = ref('g')
    const debounced = useDebouncedRef(source, 300)

    source.value = 'go'; await nextTick()
    vi.advanceTimersByTime(200)
    source.value = 'goodbye'; await nextTick()
    vi.advanceTimersByTime(200)
    expect(debounced.value).toBe('g')           // 400 ms au total, mais jamais 300 ms de calme

    vi.advanceTimersByTime(100)
    expect(debounced.value).toBe('goodbye')
  })
})