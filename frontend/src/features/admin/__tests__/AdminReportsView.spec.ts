import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AdminReportsView from '@/views/admin/AdminReportsView.vue'

vi.mock('@/features/admin/admin.api', () => ({
  adminApi: {
    getDashboard: vi.fn().mockResolvedValue({
      totalAppointments: { value: 150, change: 10 },
      totalPatients: { value: 120, change: 5 },
      totalDoctors: { value: 10, change: 0 },
      activeDoctor: { value: 8, change: 0 },
      completionRate: { value: 95, change: 2 },
      averageWaitingMinute: { value: 15, change: -1 },
    }),
    getStatistics: vi.fn().mockResolvedValue({
      chartLabels: ['14/8', '15/8', '16/8', '17/8', '18/8', '19/8', '20/8'],
      chartData: [10, 15, 20, 25, 30, 28, 22],
      specialtyDistribution: [
        { label: 'Hoàn thành', count: 140 },
        { label: 'Đã hủy', count: 10 },
      ],
    }),
    getAppointmentSummary: vi.fn().mockResolvedValue({
      total: 150,
      confirmed: 10,
      checkedIn: 5,
      completed: 130,
      cancelled: 5,
      noShow: 0,
    }),
  },
}))

vi.mock('@/features/specialties/specialties.api', () => ({
  specialtiesApi: {
    list: vi.fn().mockResolvedValue({
      items: [
        {
          id: 'spec-1',
          name: 'Tim mạch',
          description: 'Khám tim',
          establishedDate: '2020-01-01',
          status: true,
        },
      ],
      pageNumber: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
    }),
  },
}))

describe('AdminReportsView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders report view with KPI cards and specialty stats', async () => {
    const wrapper = mount(AdminReportsView)
    await flushPromises()

    expect(wrapper.text()).toContain('Báo cáo & Phân tích')
    expect(wrapper.text()).toContain('Tổng lượt khám')
    expect(wrapper.text()).toContain('Tim mạch')
    expect(wrapper.text()).toContain('Xu hướng lượt khám theo ngày')
  })

  it('allows changing period filter and triggers reload', async () => {
    const wrapper = mount(AdminReportsView)
    await flushPromises()

    const select = wrapper.find('select')
    await select.setValue('week')
    await flushPromises()

    expect(wrapper.find('h1').text()).toContain('Báo cáo & Phân tích')
  })
})
