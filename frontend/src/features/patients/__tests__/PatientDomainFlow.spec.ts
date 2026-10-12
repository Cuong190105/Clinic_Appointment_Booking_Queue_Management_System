import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { patientApi } from '@/features/patients/patient.api'
import { usePatientStore } from '@/stores/patient'
import PatientQueueView from '@/views/patient/PatientQueueView.vue'

vi.mock('@/features/patients/patient.api', () => ({
  patientApi: {
    getMyAppointments: vi.fn(),
    cancelAppointment: vi.fn(),
    getMyQueue: vi.fn(),
    getMedicalHistory: vi.fn(),
  },
}))

describe('patient domain flow', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads medical history and stores records in the patient store', async () => {
    vi.mocked(patientApi.getMedicalHistory).mockResolvedValue([
      {
        id: 1,
        examinationDate: '2026-08-11',
        doctorName: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        diagnosis: 'Viêm họng',
        prescription: 'Paracetamol 500mg',
        note: 'Nghỉ ngơi và uống đủ nước',
      },
    ])

    const patient = usePatientStore()
    await patient.loadHistory()

    expect(patientApi.getMedicalHistory).toHaveBeenCalledTimes(1)
    expect(patient.history).toHaveLength(1)
    expect(patient.history?.[0]?.diagnosis).toBe('Viêm họng')
    expect(patient.historyError).toBeNull()
  })

  it('surfaces a history error when the medical history API fails', async () => {
    vi.mocked(patientApi.getMedicalHistory).mockRejectedValue({
      response: { data: { message: 'Không thể tải lịch sử khám' } },
    })

    const patient = usePatientStore()
    await patient.loadHistory()

    expect(patient.history).toEqual([])
    expect(patient.historyError).toBe('Không thể tải lịch sử khám')
  })

  it('surfaces a history error when the medical history API fails', async () => {
    vi.mocked(patientApi.getMedicalHistory).mockRejectedValue({
      response: { data: { message: 'Không thể tải lịch sử khám' } },
    })

    const patient = usePatientStore()
    await patient.loadHistory()

    expect(patient.history).toEqual([])
    expect(patient.historyError).toBe('Không thể tải lịch sử khám')
  })

  it('marks an appointment as cancelled after a successful cancellation request', async () => {
    vi.mocked(patientApi.getMyAppointments).mockResolvedValue([
      {
        id: 88,
        doctorId: 1,
        doctorName: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        appointmentDate: '2026-08-20',
        appointmentTime: '08:00',
        status: 'Pending',
        queueNumber: 'A-12',
      },
    ])
    vi.mocked(patientApi.cancelAppointment).mockResolvedValue(undefined)

    const patient = usePatientStore()
    await patient.loadAppointments()
    await patient.cancelAppointment(88)

    expect(patientApi.cancelAppointment).toHaveBeenCalledWith(88)
    expect(patient.appointments?.[0]?.status).toBe('Cancelled')
    expect(patient.appointmentsError).toBeNull()
  })

  it('keeps the cancel state and exposes the error when cancellation fails', async () => {
    vi.mocked(patientApi.getMyAppointments).mockResolvedValue([
      {
        id: 99,
        doctorId: 2,
        doctorName: 'BS. Lê Hồng Đăng',
        specialty: 'Tim mạch',
        appointmentDate: '2026-08-21',
        appointmentTime: '09:30',
        status: 'Confirmed',
      },
    ])
    vi.mocked(patientApi.cancelAppointment).mockRejectedValue({
      response: { data: { message: 'Không thể hủy lịch' } },
    })

    const patient = usePatientStore()
    await patient.loadAppointments()

    await expect(patient.cancelAppointment(99)).rejects.toBeDefined()
    expect(patient.appointments?.[0]?.status).toBe('Confirmed')
    expect(patient.appointmentsError).toBe('Không thể hủy lịch')
  })

  it('does not expose a queue-cancel action on the current queue screen because the feature is not implemented', async () => {
    const patient = usePatientStore()
    patient.queue = {
      myTicket: 'A-12',
      position: 2,
      estimatedWaitMinutes: 5,
      doctorName: 'BS. Trần Minh Hùng',
      appointmentTime: '08:00',
      currentTicket: 'A-10',
      entries: [
        {
          ticket: 'A-10',
          patientName: 'Nguyễn Văn B',
          doctorId: 1,
          doctorName: 'BS. Trần Minh Hùng',
          appointmentTime: '08:00',
          status: 'Waiting',
          estimatedWaitMinutes: 5,
          position: 1,
          urgent: false,
        },
        {
          ticket: 'A-12',
          patientName: 'Nguyễn Văn A',
          doctorId: 1,
          doctorName: 'BS. Trần Minh Hùng',
          appointmentTime: '08:00',
          status: 'Waiting',
          estimatedWaitMinutes: 5,
          position: 2,
          urgent: false,
        },
      ],
    }

    const wrapper = mount(PatientQueueView)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Hủy hàng đợi')
    expect(wrapper.text()).not.toContain('Rời hàng đợi')
  })
})
