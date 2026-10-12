import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BasePasswordInput from '../BasePasswordInput.vue'

describe('BasePasswordInput', () => {
  it('defaults to password type', () => {
    const wrapper = mount(BasePasswordInput, {
      props: {
        modelValue: 'secret123',
      },
    })
    const input = wrapper.find('input')
    expect(input.attributes('type')).toBe('password')
  })

  it('toggles password visibility when clicking eye button', async () => {
    const wrapper = mount(BasePasswordInput, {
      props: {
        modelValue: 'secret123',
      },
    })
    const toggleButton = wrapper.find('button')
    expect(toggleButton.exists()).toBe(true)

    // Click to show password
    await toggleButton.trigger('click')
    expect(wrapper.find('input').attributes('type')).toBe('text')

    // Click again to hide password
    await toggleButton.trigger('click')
    expect(wrapper.find('input').attributes('type')).toBe('password')
  })

  it('displays error message correctly', () => {
    const wrapper = mount(BasePasswordInput, {
      props: {
        error: 'Mật khẩu không hợp lệ',
        modelValue: '',
      },
    })
    expect(wrapper.text()).toContain('Mật khẩu không hợp lệ')
  })
})
