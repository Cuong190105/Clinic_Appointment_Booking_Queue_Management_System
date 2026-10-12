import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseAlert from '../BaseAlert.vue'

describe('BaseAlert', () => {
  it('renders default error alert with message', () => {
    const wrapper = mount(BaseAlert, {
      props: {
        type: 'error',
        message: 'Tài khoản không tồn tại',
      },
    })
    expect(wrapper.text()).toContain('Tài khoản không tồn tại')
    expect(wrapper.classes()).toContain('bg-red-50')
  })

  it('renders success alert with title and slot content', () => {
    const wrapper = mount(BaseAlert, {
      props: {
        type: 'success',
        title: 'Thành công',
      },
      slots: {
        default: 'Đăng ký tài khoản thành công',
      },
    })
    expect(wrapper.text()).toContain('Thành công')
    expect(wrapper.text()).toContain('Đăng ký tài khoản thành công')
    expect(wrapper.classes()).toContain('bg-emerald-50')
  })

  it('emits dismiss event when clicking close button', async () => {
    const wrapper = mount(BaseAlert, {
      props: {
        message: 'Có thể đóng',
        dismissible: true,
      },
    })
    const closeBtn = wrapper.find('button')
    expect(closeBtn.exists()).toBe(true)
    await closeBtn.trigger('click')
    expect(wrapper.emitted('dismiss')).toHaveLength(1)
  })
})
