import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseModal from '../BaseModal.vue'

describe('BaseModal', () => {
  it('renders modal content when open is true', () => {
    const wrapper = mount(BaseModal, {
      props: { open: true, title: 'Tiêu đề Modal' },
      global: {
        stubs: { Teleport: true },
      },
      slots: {
        default: '<div class="test-body">Nội dung modal</div>',
      },
    })
    expect(wrapper.text()).toContain('Tiêu đề Modal')
    expect(wrapper.text()).toContain('Nội dung modal')
  })

  it('emits close event when clicking close button', async () => {
    const wrapper = mount(BaseModal, {
      props: { open: true, title: 'Tiêu đề' },
      global: {
        stubs: { Teleport: true },
      },
    })

    const closeBtn = wrapper.find('button[aria-label="Đóng"]')
    expect(closeBtn.exists()).toBe(true)
    await closeBtn.trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
