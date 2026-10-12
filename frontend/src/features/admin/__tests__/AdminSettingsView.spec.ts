import { describe, expect, it, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AdminSettingsView from '@/views/admin/AdminSettingsView.vue'

describe('AdminSettingsView', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('renders settings sections properly', async () => {
    const wrapper = mount(AdminSettingsView)
    await flushPromises()

    expect(wrapper.text()).toContain('Cài đặt Hệ thống')
    expect(wrapper.text()).toContain('Thông tin Phòng khám')
    expect(wrapper.text()).toContain('Quy tắc Đặt lịch & Tiếp đón Bệnh nhân')
    expect(wrapper.text()).toContain('Âm thanh & Thông báo Hàng đợi')
  })

  it('saves settings to localStorage on save click', async () => {
    const wrapper = mount(AdminSettingsView)
    await flushPromises()

    const saveBtn = wrapper.findAll('button').find((b) => b.text().includes('Lưu cấu hình'))
    expect(saveBtn).toBeDefined()

    await saveBtn!.trigger('click')
    await flushPromises()

    const saved = localStorage.getItem('clinic_admin_settings')
    expect(saved).not.toBeNull()
    const parsed = JSON.parse(saved!)
    expect(parsed.clinicName).toBe('Phòng khám Đa khoa MediCare')
  })
})
