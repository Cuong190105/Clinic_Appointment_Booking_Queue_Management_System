import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ChangePasswordModal from '../ChangePasswordModal.vue'
import { authApi } from '@/features/auth/auth.api'

describe('ChangePasswordModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not render when open is false', () => {
    const wrapper = mount(ChangePasswordModal, {
      props: { open: false },
    })
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('renders modal with 3 password inputs when open is true', () => {
    const wrapper = mount(ChangePasswordModal, {
      props: { open: true },
    })
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Đổi mật khẩu')
    const inputs = wrapper.findAll('input[type="password"]')
    expect(inputs).toHaveLength(3)
  })

  it('validates password requirements before submitting', async () => {
    const wrapper = mount(ChangePasswordModal, {
      props: { open: true },
    })

    await wrapper.find('form').trigger('submit.prevent')
    expect(wrapper.text()).toContain('Mật khẩu hiện tại')
  })

  it('submits changePassword successfully and emits events', async () => {
    vi.spyOn(authApi, 'changePassword').mockResolvedValueOnce({
      message: 'Mật khẩu đã được cập nhật thành công.',
    })

    const wrapper = mount(ChangePasswordModal, {
      props: { open: true },
    })

    const inputs = wrapper.findAll('input')
    await inputs[0]?.setValue('OldPassword123!')
    await inputs[1]?.setValue('NewPassword123!')
    await inputs[2]?.setValue('NewPassword123!')

    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(authApi.changePassword).toHaveBeenCalledWith({
      currentPassword: 'OldPassword123!',
      newPassword: 'NewPassword123!',
    })
    expect(wrapper.emitted('success')).toBeTruthy()
    expect(wrapper.text()).toContain('Mật khẩu đã được cập nhật thành công.')
  })
})
