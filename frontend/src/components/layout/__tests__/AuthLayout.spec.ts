import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import AuthLayout from '../AuthLayout.vue'

describe('AuthLayout', () => {
  it('renders default brand title and features in hero section', () => {
    const wrapper = mount(AuthLayout, {
      props: {
        title: 'Đăng nhập hệ thống',
        subtitle: 'Nhập thông tin bên dưới',
      },
      slots: {
        default: '<form id="test-form"><input type="text" /></form>',
      },
    })

    expect(wrapper.text()).toContain('ClinicQueue')
    expect(wrapper.text()).toContain('Đăng nhập hệ thống')
    expect(wrapper.text()).toContain('Nhập thông tin bên dưới')
    expect(wrapper.find('#test-form').exists()).toBe(true)
  })

  it('renders custom header and alerts slot', () => {
    const wrapper = mount(AuthLayout, {
      slots: {
        alerts: '<div class="test-alert">Lỗi đăng nhập</div>',
        footer: '<div class="test-footer">Đăng ký tại đây</div>',
      },
    })

    expect(wrapper.find('.test-alert').exists()).toBe(true)
    expect(wrapper.text()).toContain('Lỗi đăng nhập')
    expect(wrapper.find('.test-footer').exists()).toBe(true)
  })
})
