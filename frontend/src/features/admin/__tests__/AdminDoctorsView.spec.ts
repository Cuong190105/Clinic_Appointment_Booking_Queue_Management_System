import { describe, expect, it, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AdminDoctorsView from '@/views/admin/AdminDoctorsView.vue'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { specialtiesApi } from '@/features/specialties/specialties.api'

vi.mock('@/features/doctors/doctors.api', () => ({
  doctorsApi: {
    list: vi.fn().mockResolvedValue({
      items: [
        {
          id: 'doc-1',
          fullName: 'BS. Nguyễn Văn A',
          currentSpecialty: 'Tim mạch',
          licenseNumber: 'CCHN-001',
          qualification: 'Thạc sĩ',
          experienceYears: 5,
          status: 0,
          phoneNumber: '0912345678',
        },
      ],
      pageNumber: 1,
      pageSize: 8,
      totalCount: 1,
      totalPages: 1,
    }),
    create: vi.fn().mockResolvedValue({ id: 'doc-new' }),
    update: vi.fn().mockResolvedValue(undefined),
    delete: vi.fn().mockResolvedValue(undefined),
  },
}))

vi.mock('@/features/specialties/specialties.api', () => ({
  specialtiesApi: {
    list: vi.fn().mockResolvedValue({
      items: [{ id: 'spec-1', name: 'Tim mạch' }],
      totalCount: 1,
    }),
  },
}))

describe('AdminDoctorsView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('mounts and displays doctor list from API', async () => {
    const wrapper = mount(AdminDoctorsView)
    await flushPromises()

    expect(wrapper.text()).toContain('Quản lý Bác sĩ')
    expect(wrapper.text()).toContain('BS. Nguyễn Văn A')
    expect(wrapper.text()).toContain('Tim mạch')
    expect(wrapper.text()).toContain('CCHN-001')
  })

  it('filters doctors when typing search query', async () => {
    const wrapper = mount(AdminDoctorsView)
    await flushPromises()

    const searchInput = wrapper.find('input[type="text"]')
    expect(searchInput.exists()).toBe(true)

    await searchInput.setValue('Nguyễn Văn')
    await flushPromises()

    expect(doctorsApi.list).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'Nguyễn Văn' }),
    )
  })
})
