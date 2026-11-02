import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePatientStore } from '@/stores/patient'
import { useAuthStore } from '@/stores/auth'
import { patientApi } from '@/features/patients/patient.api'

vi.mock('@/features/patients/patient.api', () => ({
  patientApi: {
    getDoctors: vi.fn(),
    getAvailableSlots: vi.fn(),
    createAppointment: vi.fn(),
    getMyAppointments: vi.fn(),
    cancelAppointment: vi.fn(),
    getMyQueue: vi.fn(),
    getMedicalHistory: vi.fn(),
    getMyProfile: vi.fn(),
  },
}))

describe('Patient Store Unit Tests (Edge Cases & Workflows)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('khởi tạo store với trạng thái mặc định', () => {
    const store = usePatientStore()
    expect(store.doctors).toEqual([])
    expect(store.appointments).toEqual([])
    expect(store.queue).toBeNull()
    expect(store.history).toEqual([])
    expect(store.profile).toBeNull()
  })

  describe('Sắp xếp lịch sử khám (sortMedicalHistory edge cases)', () => {
    it('sắp xếp chính xác theo ngày giảm dần với nhiều định dạng (DD/MM/YYYY, ISO)', async () => {
      const mockRecords = [
        { id: 1, examinationDate: '15/01/2026', doctorName: 'BS Nam', diagnosis: 'Cảm cúm' },
        { id: 2, examinationDate: '2026-03-10', doctorName: 'BS Hoa', diagnosis: 'Đau dạ dày' },
        { id: 3, examinationDate: '01/02/2026', doctorName: 'BS Tuấn', diagnosis: 'Đau đầu' },
      ]

      vi.mocked(patientApi.getMedicalHistory).mockResolvedValue(mockRecords as any)

      const store = usePatientStore()
      await store.loadHistory()

      // Thứ tự mong đợi: 2026-03-10 (mới nhất), 01/02/2026, 15/01/2026
      expect(store.history).toHaveLength(3)
      expect(store.history[0]?.id).toBe(2)
      expect(store.history[1]?.id).toBe(3)
      expect(store.history[2]?.id).toBe(1)
    })

    it('xử lý bản ghi có ngày không hợp lệ hoặc thiếu ngày mà không gây crash', async () => {
      const mockRecords = [
        { id: 1, examinationDate: 'invalid-date', doctorName: 'BS A' },
        { id: 2, examinationDate: '20/05/2026', doctorName: 'BS B' },
        { id: 3, examinationDate: null, doctorName: 'BS C' },
      ]

      vi.mocked(patientApi.getMedicalHistory).mockResolvedValue(mockRecords as any)

      const store = usePatientStore()
      await store.loadHistory()

      // Bản ghi có ngày hợp lệ (id: 2) phải được xếp lên đầu
      expect(store.history[0]?.id).toBe(2)
    })
  })

  describe('Lịch hẹn & Hủy lịch (Appointments & Cancellation)', () => {
    it('upcomingAppointments chỉ lọc các lịch hẹn có trạng thái Pending, Confirmed, CheckedIn', async () => {
      const mockAppointments = [
        { id: 'apt-1', status: 'Pending', appointmentDate: '2026-11-05' },
        { id: 'apt-2', status: 'Confirmed', appointmentDate: '2026-11-06' },
        { id: 'apt-3', status: 'Completed', appointmentDate: '2026-10-01' },
        { id: 'apt-4', status: 'Cancelled', appointmentDate: '2026-10-02' },
        { id: 'apt-5', status: 'CheckedIn', appointmentDate: '2026-11-05' },
      ]

      vi.mocked(patientApi.getMyAppointments).mockResolvedValue(mockAppointments as any)

      const store = usePatientStore()
      await store.loadAppointments()

      expect(store.appointments).toHaveLength(5)
      expect(store.upcomingAppointments).toHaveLength(3)
      const upcomingIds = store.upcomingAppointments.map((a) => a.id)
      expect(upcomingIds).toEqual(['apt-1', 'apt-2', 'apt-5'])
    })

    it('cancelAppointment gọi API và cập nhật trạng thái lịch hẹn thành Cancelled', async () => {
      const mockAppointments = [
        { id: 'apt-1', status: 'Confirmed', appointmentDate: '2026-11-05' },
      ]
      vi.mocked(patientApi.getMyAppointments).mockResolvedValue(mockAppointments as any)
      vi.mocked(patientApi.cancelAppointment).mockResolvedValue({} as any)

      const store = usePatientStore()
      await store.loadAppointments()
      await store.cancelAppointment('apt-1')

      expect(patientApi.cancelAppointment).toHaveBeenCalledWith('apt-1')
      expect(store.appointments[0]?.status).toBe('Cancelled')
    })
  })

  describe('Hàng đợi của bệnh nhân (Queue)', () => {
    it('loadQueue lấy thông tin số thứ tự khám của tôi', async () => {
      const mockQueue = {
        myTicket: 'A-015',
        position: 3,
        estimatedWaitMinutes: 15,
        currentTicket: 'A-012',
      }
      vi.mocked(patientApi.getMyQueue).mockResolvedValue(mockQueue as any)

      const store = usePatientStore()
      await store.loadQueue()

      expect(store.queue?.myTicket).toBe('A-015')
      expect(store.queue?.position).toBe(3)
    })

    it('loadQueue gán error và đặt queue về null khi API thất bại', async () => {
      vi.mocked(patientApi.getMyQueue).mockRejectedValueOnce({
        response: { data: { message: 'Không tìm thấy lượt khám' } },
      })

      const store = usePatientStore()
      await store.loadQueue()

      expect(store.queue).toBeNull()
      expect(store.queueError).toBe('Không tìm thấy lượt khám')
    })
  })

  describe('Hồ sơ bệnh nhân & Đồng bộ Auth', () => {
    it('loadProfile cập nhật profile và đồng bộ tên hiển thị sang authStore', async () => {
      const auth = useAuthStore()
      auth.setUser({ id: 'u-1', name: 'User Cu', email: 'cu@gmail.com', role: 'Patient' } as any)

      const profileData = {
        id: 'pat-1',
        fullName: 'Nguyễn Văn Mới',
        email: 'moi@gmail.com',
      }
      vi.mocked(patientApi.getMyProfile).mockResolvedValue(profileData as any)

      const store = usePatientStore()
      await store.loadProfile()

      expect(store.profile?.fullName).toBe('Nguyễn Văn Mới')
      expect(auth.user?.name).toBe('Nguyễn Văn Mới')
      expect(auth.user?.email).toBe('moi@gmail.com')
    })

    it('reset khôi phục toàn bộ trạng thái store về rỗng', () => {
      const store = usePatientStore()
      store.doctors = [{ id: 1, name: 'BS A' } as any]
      store.queue = { myTicket: 'A-1' } as any
      store.appointmentsError = 'Lỗi'

      store.reset()

      expect(store.doctors).toEqual([])
      expect(store.queue).toBeNull()
      expect(store.appointmentsError).toBeNull()
    })
  })
})
