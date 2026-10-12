import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ForgotPasswordView from '../ForgotPasswordView.vue'
import { authApi } from '@/features/auth/auth.api'

const { routerPush } = vi.hoisted(() => ({
  routerPush: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: routerPush,
  }),
}))

describe('ForgotPasswordView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders title and input field', () => {
    const wrapper = mount(ForgotPasswordView)
    expect(wrapper.text()).toContain('Quên mật khẩu')
    expect(wrapper.find('input').exists()).toBe(true)
  })

  it('validates empty input on submit', async () => {
    const wrapper = mount(ForgotPasswordView)
    await wrapper.find('form').trigger('submit.prevent')
    expect(wrapper.text()).toContain('Email hoặc Số điện thoại')
  })

  it('calls authApi.forgotPassword and displays success state', async () => {
    vi.spyOn(authApi, 'forgotPassword').mockResolvedValueOnce({
      message: 'Mật khẩu tạm thời đã được gửi.',
    })

    const wrapper = mount(ForgotPasswordView)
    const input = wrapper.find('input')
    await input.setValue('patient@example.com')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(authApi.forgotPassword).toHaveBeenCalledWith('patient@example.com')
    expect(wrapper.text()).toContain('Yêu cầu đã được xử lý!')
    expect(wrapper.text()).toContain('Mật khẩu tạm thời đã được gửi.')
  })

  it('displays error alert if API fails', async () => {
    vi.spyOn(authApi, 'forgotPassword').mockRejectedValueOnce(
      new Error('Không tìm thấy tài khoản.'),
    )

    const wrapper = mount(ForgotPasswordView)
    const input = wrapper.find('input')
    await input.setValue('0987654321')
    await wrapper.find('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('Không tìm thấy tài khoản.')
  })
})
