import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AdminSpecialtiesView from '@/views/admin/AdminSpecialtiesView.vue'
import { specialtiesApi } from '@/features/specialties/specialties.api'

vi.mock('@/features/specialties/specialties.api', () => ({
  specialtiesApi: {
    list: vi.fn().mockResolvedValue({
      items: [
        {
          id: 'spec-1',
          name: 'Khoa Nhi',
          description: 'Khám nhi toàn diện',
          establishedDate: '2022-05-15',
          status: true,
        },
      ],
      pageNumber: 1,
      pageSize: 8,
      totalCount: 1,
      totalPages: 1,
    }),
    create: vi.fn().mockResolvedValue({ id: 'spec-new' }),
    update: vi.fn().mockResolvedValue(undefined),
    toggleStatus: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('AdminSpecialtiesView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('mounts and displays specialties from API', async () => {
    const wrapper = mount(AdminSpecialtiesView)
    await flushPromises()

    expect(wrapper.text()).toContain('Quản lý Chuyên khoa')
    expect(wrapper.text()).toContain('Khoa Nhi')
    expect(wrapper.text()).toContain('Khám nhi toàn diện')
  })
})
