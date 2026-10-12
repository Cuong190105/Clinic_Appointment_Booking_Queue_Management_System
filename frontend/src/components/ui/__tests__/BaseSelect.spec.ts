import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseSelect from '../BaseSelect.vue'

describe('BaseSelect', () => {
  it('renders options from string array and object array', () => {
    const wrapper = mount(BaseSelect, {
      props: {
        label: 'Giới tính',
        options: [
          { label: 'Nam', value: 'Male' },
          { label: 'Nữ', value: 'Female' },
        ],
        modelValue: 'Male',
      },
    })
    expect(wrapper.text()).toContain('Giới tính')
    const options = wrapper.findAll('option')
    expect(options).toHaveLength(2)
    expect(options[0]?.text()).toBe('Nam')
    expect(options[1]?.text()).toBe('Nữ')
  })

  it('updates modelValue on change', async () => {
    const wrapper = mount(BaseSelect, {
      props: {
        options: ['A', 'B'],
        modelValue: 'A',
        'onUpdate:modelValue': (e: string | number) => wrapper.setProps({ modelValue: e }),
      },
    })
    const select = wrapper.find('select')
    await select.setValue('B')
    expect(wrapper.props('modelValue')).toBe('B')
  })
})
