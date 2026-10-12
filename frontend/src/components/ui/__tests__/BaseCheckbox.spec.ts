import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseCheckbox from '../BaseCheckbox.vue'

describe('BaseCheckbox', () => {
  it('renders label and description', () => {
    const wrapper = mount(BaseCheckbox, {
      props: {
        label: 'Ghi nhớ đăng nhập',
        description: 'Lưu phiên trên thiết bị này',
        modelValue: false,
      },
    })
    expect(wrapper.text()).toContain('Ghi nhớ đăng nhập')
    expect(wrapper.text()).toContain('Lưu phiên trên thiết bị này')
  })

  it('toggles value on input change', async () => {
    const wrapper = mount(BaseCheckbox, {
      props: {
        label: 'Ghi nhớ',
        modelValue: false,
        'onUpdate:modelValue': (e: boolean) => wrapper.setProps({ modelValue: e }),
      },
    })
    const input = wrapper.find('input[type="checkbox"]')
    await input.setValue(true)
    expect(wrapper.props('modelValue')).toBe(true)
  })

  it('is disabled when disabled prop is true', () => {
    const wrapper = mount(BaseCheckbox, {
      props: {
        label: 'Ghi nhớ',
        disabled: true,
      },
    })
    const input = wrapper.find('input[type="checkbox"]')
    expect(input.attributes('disabled')).toBeDefined()
    expect(wrapper.find('label').classes()).toContain('cursor-not-allowed')
  })
})
