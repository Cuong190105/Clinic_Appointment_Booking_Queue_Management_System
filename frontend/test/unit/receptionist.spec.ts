import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useReceptionistStore } from '@/stores/receptionist'
import { receptionistApi } from '@/features/receptionist/receptionist.api'

vi.mock('@/features/receptionist/receptionist.api', () => ({
  receptionistApi: {
    getDoctorQueue: vi.fn(),
    callTicket: vi.fn(),
    skipTicket: vi.fn(),
    searchPatients: vi.fn(),
    getUpcomingAppointments: vi.fn(),
    confirmAppointment: vi.fn(),
    checkInAppointment: vi.fn(),
  },
}))

describe('Receptionist Store & Workflow (Mock-free)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('khởi tạo store với hàng đợi rỗng và không có mock data', () => {
    const store = useReceptionistStore()
    expect(store.queue).toEqual([])
    expect(store.waitingCount).toBe(0)
    expect(store.examiningCount).toBe(0)
    expect(store.completedCount).toBe(0)
  })

  it('fetchQueue tải danh sách hàng đợi từ receptionistApi', async () => {
    const mockTickets = [
      {
        id: 't-1',
        appointmentId: 'a-1',
        queueNumber: 1,
        patientName: 'Nguyễn Văn A',
        doctorName: 'BS Nam',
        status: 'Waiting',
        checkInTime: '2026-08-21T08:30:00Z',
      },
      {
        id: 't-2',
        appointmentId: 'a-2',
        queueNumber: 2,
        patientName: 'Trần Thị B',
        doctorName: 'BS Nam',
        status: 'Called',
        checkInTime: '2026-08-21T08:45:00Z',
      },
    ]

    vi.mocked(receptionistApi.getDoctorQueue).mockResolvedValue(mockTickets)

    const store = useReceptionistStore()
    await store.fetchQueue('doc-123')

    expect(receptionistApi.getDoctorQueue).toHaveBeenCalledWith('doc-123')
    expect(store.queue.length).toBe(2)
    expect(store.queue[0].no).toBe('A-001')
    expect(store.queue[0].status).toBe('waiting')
    expect(store.queue[1].no).toBe('A-002')
    expect(store.queue[1].status).toBe('called')
    expect(store.waitingCount).toBe(1)
    expect(store.examiningCount).toBe(1)
  })

  it('callPatient gọi receptionistApi.callTicket và làm mới hàng đợi', async () => {
    const mockTickets = [
      {
        id: 't-1',
        appointmentId: 'a-1',
        queueNumber: 1,
        patientName: 'Nguyễn Văn A',
        status: 'Waiting',
        checkInTime: '2026-08-21T08:30:00Z',
      },
    ]

    vi.mocked(receptionistApi.getDoctorQueue).mockResolvedValue(mockTickets)
    vi.mocked(receptionistApi.callTicket).mockResolvedValue(mockTickets[0] as any)

    const store = useReceptionistStore()
    await store.fetchQueue('doc-123')
    await store.callPatient('t-1')

    expect(receptionistApi.callTicket).toHaveBeenCalledWith('t-1')
  })

  it('skipPatient gọi receptionistApi.skipTicket và làm mới hàng đợi', async () => {
    const mockTickets = [
      {
        id: 't-1',
        appointmentId: 'a-1',
        queueNumber: 1,
        patientName: 'Nguyễn Văn A',
        status: 'Waiting',
        checkInTime: '2026-08-21T08:30:00Z',
      },
    ]

    vi.mocked(receptionistApi.getDoctorQueue).mockResolvedValue(mockTickets)
    vi.mocked(receptionistApi.skipTicket).mockResolvedValue()

    const store = useReceptionistStore()
    await store.fetchQueue('doc-123')
    await store.skipPatient('t-1')

    expect(receptionistApi.skipTicket).toHaveBeenCalledWith('t-1')
  })
})
