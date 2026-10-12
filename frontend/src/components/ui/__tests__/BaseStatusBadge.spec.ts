import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseStatusBadge from '../BaseStatusBadge.vue'

describe('BaseStatusBadge', () => {
  it('renders active status correctly with green styling', () => {
    const wrapper = mount(BaseStatusBadge, {
      props: { status: 'active' },
    })
    expect(wrapper.text()).toContain('Đang hoạt động')
    expect(wrapper.classes()).toContain('text-emerald-700')
  })

  it('renders inactive status correctly', () => {
    const wrapper = mount(BaseStatusBadge, {
      props: { status: 'inactive' },
    })
    expect(wrapper.text()).toContain('Ngừng hoạt động')
  })

  it('renders custom label override when provided', () => {
    const wrapper = mount(BaseStatusBadge, {
      props: { status: 'active', label: 'Tùy chỉnh' },
    })
    expect(wrapper.text()).toContain('Tùy chỉnh')
  })

  it('handles boolean status', () => {
    const wrapper = mount(BaseStatusBadge, {
      props: { status: true },
    })
    expect(wrapper.text()).toContain('Hoạt động')
  })
})
