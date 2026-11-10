import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ForgotPasswordView from '../ForgotPasswordView.vue'
import { authApi } from '@/features/auth/auth.api'

const { routerPush } = vi.hoisted(() => ({ routerPush: vi.fn() }))

vi.mock('vue-router', () => ({ useRouter: () => ({ push: routerPush }) }))

describe('ForgotPasswordView', () => {
  beforeEach(() => vi.clearAllMocks())

  it('requests a reset code and lets the user submit a new password', async () => {
    vi.spyOn(authApi, 'forgotPassword').mockResolvedValueOnce({ message: 'If the account exists, a code was sent.' })
    vi.spyOn(authApi, 'resetPassword').mockResolvedValueOnce({ message: 'Password updated.' })

    const wrapper = mount(ForgotPasswordView)
    await wrapper.find('input').setValue('patient@example.com')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(authApi.forgotPassword).toHaveBeenCalledWith('patient@example.com')
    expect(wrapper.text()).toContain('Nhập mã đã nhận')

    const inputs = wrapper.findAll('input')
    await inputs[0]!.setValue('one-time-code')
    await inputs[1]!.setValue('NewPassword123')
    await inputs[2]!.setValue('NewPassword123')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(authApi.resetPassword).toHaveBeenCalledWith({ token: 'one-time-code', newPassword: 'NewPassword123' })
    expect(wrapper.text()).toContain('Password updated.')
  })

  it('requires matching passwords before calling the reset API', async () => {
    const resetSpy = vi.spyOn(authApi, 'resetPassword')
    vi.spyOn(authApi, 'forgotPassword').mockResolvedValueOnce({ message: 'If the account exists, a code was sent.' })
    const wrapper = mount(ForgotPasswordView)
    await wrapper.find('input').setValue('patient@example.com')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()
    const inputs = wrapper.findAll('input')
    await inputs[0]!.setValue('one-time-code')
    await inputs[1]!.setValue('NewPassword123')
    await inputs[2]!.setValue('DifferentPassword123')
    await wrapper.find('form').trigger('submit.prevent')
    expect(wrapper.text()).toContain('Mật khẩu xác nhận không khớp.')
    expect(resetSpy).not.toHaveBeenCalled()
  })
})
