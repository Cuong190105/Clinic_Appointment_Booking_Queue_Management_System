import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const patientStore = vi.hoisted(() => ({
  history: [] as any[],
  historyLoading: false,
  historyError: null as string | null,
  loadHistory: vi.fn(),
}))

vi.mock('@/stores/patient', () => ({
  usePatientStore: () => patientStore,
}))

import PatientHistoryView from '@/views/patient/PatientHistoryView.vue'

describe('PatientHistoryView', () => {
  beforeEach(() => {
    patientStore.history = []
    patientStore.historyLoading = false
    patientStore.historyError = null
    patientStore.loadHistory.mockReset()
  })

  it('loads history when the view mounts', async () => {
    mount(PatientHistoryView)

    await flushPromises()

    expect(patientStore.loadHistory).toHaveBeenCalledTimes(1)
  })

  it('shows an error state when loading history fails', async () => {
    patientStore.historyError = 'Không thể tải lịch sử khám'

    const wrapper = mount(PatientHistoryView)
    await flushPromises()

    expect(wrapper.text()).toContain('Không thể tải lịch sử khám')
  })

  it('shows empty state when no history exists', async () => {
    const wrapper = mount(PatientHistoryView)
    await flushPromises()

    expect(wrapper.text()).toContain('Chưa có lịch sử khám.')
  })

  it('renders the examination records in a readable summary', async () => {
    patientStore.history = [
      {
        id: 1,
        examinationDate: '2026-08-11',
        doctorName: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        diagnosis: 'Viêm họng',
        prescription: 'Paracetamol 500mg',
        note: 'Uống đủ nước và nghỉ ngơi',
      },
    ]

    const wrapper = mount(PatientHistoryView)
    await flushPromises()

    expect(wrapper.text()).toContain('BS. Trần Minh Hùng')
    expect(wrapper.text()).toContain('Viêm họng')
    expect(wrapper.text()).toContain('Paracetamol 500mg')
    expect(wrapper.text()).toContain('Uống đủ nước và nghỉ ngơi')
  })
})
