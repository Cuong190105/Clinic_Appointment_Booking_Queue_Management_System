import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AppLayout from '../AppLayout.vue'
import { notificationsApi } from '@/features/notifications/notifications.api'

const mockPush = vi.fn()
const mockLogout = vi.fn()
const mockFetchMe = vi.fn().mockResolvedValue({})
const mockStopRealtime = vi.fn()

const mockAuth = {
  isAuthenticated: true,
  user: {
    id: 'u-1',
    name: 'BS Nguyen',
    activeRole: 'Doctor',
    role: 'Doctor',
  },
  currentUserRole: 'Doctor',
  fetchMe: mockFetchMe,
  logout: mockLogout,
}

const mockPatient = {
  profile: null as any,
}

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
  RouterLink: {
    template: '<a><slot /></a>',
    props: ['to'],
  },
  RouterView: {
    template: '<div><slot /></div>',
  },
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    locale: { value: 'vi' },
  }),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

vi.mock('@/stores/patient', () => ({
  usePatientStore: () => mockPatient,
}))

vi.mock('@/lib/i18n', () => ({
  supportedLocales: ['vi', 'en'],
  setLocale: vi.fn(),
}))

vi.mock('@/components/ui/ChangePasswordModal.vue', () => ({
  default: {
    template: '<div></div>',
    props: ['open'],
  },
}))

vi.mock('@/features/notifications/notifications.api', () => ({
  notificationsApi: {
    getUnreadCount: vi.fn(),
    list: vi.fn(),
    markAsRead: vi.fn(),
    markAllAsRead: vi.fn(),
    connectRealtimeHints: vi.fn(),
  },
}))

describe('AppLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAuth.isAuthenticated = true
    mockAuth.user.activeRole = 'Doctor'
    vi.mocked(notificationsApi.connectRealtimeHints).mockReturnValue({
      stop: mockStopRealtime,
    } as any)
    vi.mocked(notificationsApi.getUnreadCount).mockResolvedValue(5)
    vi.mocked(notificationsApi.list).mockResolvedValue({
      items: [
        {
          id: 'n-1',
          category: 'AppointmentConfirmed',
          title: 'Lịch khám đã xác nhận',
          content: 'Lịch khám ngày 10/11 đã được xác nhận',
          isRead: false,
          createdAt: '2026-11-01T08:00:00Z',
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 10,
      totalPages: 1,
    } as any)
  })

  it('khởi tạo kết nối realtime và hiển thị badge số lượng thông báo chưa đọc', async () => {
    const wrapper = mount(AppLayout)
    await flushPromises()

    expect(notificationsApi.connectRealtimeHints).toHaveBeenCalled()
    expect(notificationsApi.getUnreadCount).toHaveBeenCalled()
    expect(notificationsApi.list).toHaveBeenCalled()

    // Bell notification badge shows 5
    const bellBtn = wrapper.find('button[aria-label="Notifications"]')
    expect(bellBtn.exists()).toBe(true)
    expect(bellBtn.text()).toContain('5')

    wrapper.unmount()
    expect(mockStopRealtime).toHaveBeenCalled()
  })

  it('hiển thị 99+ khi số lượng thông báo vượt quá 99', async () => {
    vi.mocked(notificationsApi.getUnreadCount).mockResolvedValue(150)

    const wrapper = mount(AppLayout)
    await flushPromises()

    const bellBtn = wrapper.find('button[aria-label="Notifications"]')
    expect(bellBtn.text()).toContain('99+')
    wrapper.unmount()
  })

  it('mở danh sách dropdown khi bấm vào chuông thông báo và đánh dấu đã đọc', async () => {
    vi.mocked(notificationsApi.markAsRead).mockResolvedValue()

    const wrapper = mount(AppLayout)
    await flushPromises()

    const bellBtn = wrapper.find('button[aria-label="Notifications"]')
    await bellBtn.trigger('click')

    expect(wrapper.text()).toContain('Thông báo')
    expect(wrapper.text()).toContain('Lịch khám đã xác nhận')

    // Click item to mark as read
    const notifItem = wrapper.findAll('li').find((li) => li.text().includes('Lịch khám đã xác nhận'))
    expect(notifItem?.exists()).toBe(true)

    await notifItem!.trigger('click')
    await flushPromises()

    expect(notificationsApi.markAsRead).toHaveBeenCalledWith('n-1')
    wrapper.unmount()
  })

  it('bấm "Đánh dấu đã đọc tất cả" gọi notificationsApi.markAllAsRead', async () => {
    vi.mocked(notificationsApi.markAllAsRead).mockResolvedValue()

    const wrapper = mount(AppLayout)
    await flushPromises()

    const bellBtn = wrapper.find('button[aria-label="Notifications"]')
    await bellBtn.trigger('click')

    const markAllBtn = wrapper.findAll('button').find((b) => b.text().includes('Đánh dấu đã đọc tất cả'))
    expect(markAllBtn?.exists()).toBe(true)

    await markAllBtn!.trigger('click')
    await flushPromises()

    expect(notificationsApi.markAllAsRead).toHaveBeenCalled()
    wrapper.unmount()
  })

  it('hiển thị menu phù hợp cho vai trò Doctor', async () => {
    const wrapper = mount(AppLayout)
    await flushPromises()

    expect(wrapper.text()).toContain('Khám bệnh & hàng đợi')
    expect(wrapper.text()).toContain('Hồ sơ bệnh án')
    wrapper.unmount()
  })
})
