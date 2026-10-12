import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseInput from '../BaseInput.vue'

describe('BaseInput', () => {
  it('renders label and required asterisk', () => {
    const wrapper = mount(BaseInput, {
      props: {
        label: 'Tên đăng nhập',
        required: true,
        modelValue: '',
      },
    })
    expect(wrapper.text()).toContain('Tên đăng nhập')
    expect(wrapper.text()).toContain('*')
  })

  it('updates modelValue on input event', async () => {
    const wrapper = mount(BaseInput, {
      props: {
        modelValue: 'old',
        'onUpdate:modelValue': (e: string | number) => wrapper.setProps({ modelValue: e }),
      },
    })
    const input = wrapper.find('input')
    await input.setValue('new value')
    expect(wrapper.props('modelValue')).toBe('new value')
  })

  it('displays error message and sets aria-invalid', () => {
    const wrapper = mount(BaseInput, {
      props: {
        error: 'Vui lòng nhập tên',
        modelValue: '',
      },
    })
    expect(wrapper.text()).toContain('Vui lòng nhập tên')
    expect(wrapper.find('input').attributes('aria-invalid')).toBe('true')
  })

  it('disables input when disabled prop is true', () => {
    const wrapper = mount(BaseInput, {
      props: {
        disabled: true,
        modelValue: '',
      },
    })
    expect(wrapper.find('input').attributes('disabled')).toBeDefined()
  })
})
