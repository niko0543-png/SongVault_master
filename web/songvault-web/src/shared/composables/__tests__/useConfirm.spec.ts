import { describe, expect, it } from 'vitest'
import { useConfirm } from '../useConfirm'

describe('useConfirm', () => {
  it('résout true quand on confirme', async () => {
    const { confirm, answer } = useConfirm()
    const result = confirm({ title: 'Supprimer ?', message: 'Définitif.' })

    answer(true)

    await expect(result).resolves.toBe(true)
  })

  it('une nouvelle question annule la précédente', async () => {
    const { confirm, answer } = useConfirm()
    const first = confirm({ title: 'A', message: 'a' })
    const second = confirm({ title: 'B', message: 'b' })

    answer(true)

    await expect(first).resolves.toBe(false)
    await expect(second).resolves.toBe(true)
  })
})