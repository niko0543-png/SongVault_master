import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StatusBadge from '../components/StatusBadge.vue'
import { SONG_VERSION_STATUSES, statusLabels } from '../types'

describe('StatusBadge', () => {
  it.each(SONG_VERSION_STATUSES)('affiche un libellé et une couleur pour %s', (status) => {
    const wrapper = mount(StatusBadge, { props: { status } })
    const badge = wrapper.get('[data-testid="status-badge"]')

    expect(badge.text()).toBe(statusLabels[status])
    expect(badge.classes()).toContain(`badge--${status.toLowerCase()}`)
  })
})
