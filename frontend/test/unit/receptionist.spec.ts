import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useReceptionistStore } from '@/stores/receptionist'
import { receptionistApi } from '@/features/receptionist/receptionist.api'

vi.mock('@/features/receptionist/receptionist.api', () => ({
  receptionistApi: {
    getDoctorQueue: vi.fn(),
    getQueueDoctors: vi.fn(),
    callTicket: vi.fn(),
    skipTicket: vi.fn(),
    retryTicket: vi.fn(),
    setPriority: vi.fn(),
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
    vi.mocked(receptionistApi.callTicket).mockResolvedValue({
      id: 't-1',
      appointmentId: 'a-1',
      queueNumber: 1,
      status: 'Called',
    } as any)

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

  it('retryPatient gọi receptionistApi.retryTicket và làm mới hàng đợi', async () => {
    const mockTickets = [
      {
        id: 't-1',
        appointmentId: 'a-1',
        queueNumber: 1,
        patientName: 'Nguyễn Văn A',
        status: 'Skipped',
        checkInTime: '2026-08-21T08:30:00Z',
      },
    ]

    vi.mocked(receptionistApi.getDoctorQueue).mockResolvedValue(mockTickets)
    vi.mocked(receptionistApi.retryTicket).mockResolvedValue({
      id: 't-1',
      appointmentId: 'a-1',
      queueNumber: 1,
      status: 'Waiting',
    } as any)

    const store = useReceptionistStore()
    await store.fetchQueue('doc-123')
    await store.retryPatient('t-1')

    expect(receptionistApi.retryTicket).toHaveBeenCalledWith('t-1')
  })

  it('setPatientPriority gọi receptionistApi.setPriority và làm mới hàng đợi', async () => {
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
    vi.mocked(receptionistApi.setPriority).mockResolvedValue()

    const store = useReceptionistStore()
    await store.fetchQueue('doc-123')
    await store.setPatientPriority('t-1', true)

    expect(receptionistApi.setPriority).toHaveBeenCalledWith('t-1', true)
  })

  it('fetchQueue khi không truyền doctorId sẽ lấy danh sách bác sĩ và gộp hàng đợi', async () => {
    vi.mocked(receptionistApi.getQueueDoctors).mockResolvedValue([
      { doctorId: 'doc-1', doctorName: 'BS A' } as any,
      { doctorId: 'doc-2', doctorName: 'BS B' } as any,
    ])

    vi.mocked(receptionistApi.getDoctorQueue)
      .mockResolvedValueOnce([
        { id: 't-1', appointmentId: 'a-1', queueNumber: 10, patientName: 'Bệnh nhân 10', status: 'Waiting' } as any,
      ])
      .mockResolvedValueOnce([
        { id: 't-2', appointmentId: 'a-2', queueNumber: 2, patientName: 'Bệnh nhân 2', status: 'Waiting' } as any,
      ])

    const store = useReceptionistStore()
    await store.fetchQueue()

    expect(receptionistApi.getQueueDoctors).toHaveBeenCalled()
    expect(receptionistApi.getDoctorQueue).toHaveBeenCalledWith('doc-1')
    expect(receptionistApi.getDoctorQueue).toHaveBeenCalledWith('doc-2')
    expect(store.queue).toHaveLength(2)
    // Phải được sắp xếp theo số thứ tự số học (2 trước 10)
    expect(store.queue[0].no).toBe('A-002')
    expect(store.queue[1].no).toBe('A-010')
  })

  it('fetchQueue xử lý lỗi phân quyền 401/403 một cách phù hợp', async () => {
    const error403 = { status: 403, message: 'Forbidden' }
    vi.mocked(receptionistApi.getDoctorQueue).mockRejectedValueOnce(error403)

    const store = useReceptionistStore()
    await store.fetchQueue('doc-1')

    expect(store.queueError).toBe('Bạn không có quyền xem hàng đợi.')
    expect(store.queue).toEqual([])
  })

  it('fetchQueue xử lý lỗi chung và gán thông báo lỗi', async () => {
    vi.mocked(receptionistApi.getDoctorQueue).mockRejectedValueOnce(new Error('Mất kết nối mạng'))

    const store = useReceptionistStore()
    await store.fetchQueue('doc-1')

    expect(store.queueError).toBe('Mất kết nối mạng')
    expect(store.queue).toEqual([])
  })

  it('callPatient / skipPatient / retryPatient không lỗi khi không tìm thấy ticketId', async () => {
    const store = useReceptionistStore()
    // Hàng đợi rỗng
    await expect(store.callPatient('non-existent')).resolves.toBeUndefined()
    await expect(store.skipPatient('non-existent')).resolves.toBeUndefined()
    await expect(store.retryPatient('non-existent')).resolves.toBeUndefined()
    await expect(store.setPatientPriority('non-existent', true)).resolves.toBeUndefined()
  })

  it('cập nhật trạng thái cục bộ nếu vé không có id trên backend (mock ticket)', async () => {
    const store = useReceptionistStore()
    store.queue = [
      {
        id: '' as any,
        appointmentId: 'apt-local',
        doctorId: 'doc-1',
        no: 'A-001',
        name: 'BN Demo',
        doctor: 'BS Demo',
        time: '08:00',
        status: 'waiting',
      },
    ]

    await store.callPatient('')
    expect(store.queue[0].status).toBe('called')

    await store.skipPatient('')
    expect(store.queue[0].status).toBe('skipped')

    await store.retryPatient('')
    expect(store.queue[0].status).toBe('waiting')
  })
})
