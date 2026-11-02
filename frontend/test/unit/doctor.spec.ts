import { describe, it, expect, vi, beforeEach } from 'vitest'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { http } from '@/lib/api/http'

vi.mock('@/lib/api/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}))

describe('Doctor Unit Tests (API & Business Workflow)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('Doctor Queue Management', () => {
    it('getQueue lấy danh sách bệnh nhân đang chờ của bác sĩ', async () => {
      const mockQueue = [
        {
          id: 't-1',
          appointmentId: 'a-1',
          queueNumber: 1,
          patientName: 'Nguyen Van A',
          status: 'Waiting',
          checkInTime: '2026-11-01T08:00:00Z',
        },
        {
          id: 't-2',
          appointmentId: 'a-2',
          queueNumber: 2,
          patientName: 'Tran Thi B',
          status: 'Called',
          checkInTime: '2026-11-01T08:15:00Z',
        },
      ]

      vi.mocked(http.get).mockResolvedValueOnce({
        data: { result: mockQueue },
      } as any)

      const result = await doctorsApi.getQueue('doc-100')

      expect(http.get).toHaveBeenCalledWith('/doctors/doc-100/queue')
      expect(result).toHaveLength(2)
      expect(result[0]?.queueNumber).toBe(1)
      expect(result[0]?.status).toBe('Waiting')
    })

    it('callNext gọi bệnh nhân tiếp theo trong hàng đợi của bác sĩ', async () => {
      const nextTicket = {
        id: 't-1',
        appointmentId: 'a-1',
        queueNumber: 1,
        status: 'Called',
      }

      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: nextTicket },
      } as any)

      const result = await doctorsApi.callNext('doc-100')

      expect(http.post).toHaveBeenCalledWith('/doctors/doc-100/queue/next')
      expect(result.status).toBe('Called')
    })

    it('startExam chuyển trạng thái vé sang InProgress', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: { id: 't-1', status: 'InProgress' } },
      } as any)

      await doctorsApi.startExam('t-1')

      expect(http.post).toHaveBeenCalledWith('/queue/t-1/start-exam')
    })

    it('completeExam hoàn tất lượt khám của bệnh nhân', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: { id: 't-1', status: 'Completed' } },
      } as any)

      await doctorsApi.completeExam('t-1')

      expect(http.post).toHaveBeenCalledWith('/queue/t-1/complete-exam')
    })

    it('skip bỏ qua lượt của bệnh nhân vắng mặt', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: { success: true, message: 'Ticket skipped' } },
      } as any)

      const result = await doctorsApi.skip('t-1')

      expect(http.post).toHaveBeenCalledWith('/queue/t-1/skip')
      expect(result.success).toBe(true)
    })

    it('setPriority cập nhật cờ ưu tiên cho vé', async () => {
      vi.mocked(http.patch).mockResolvedValueOnce({
        data: { result: true },
      } as any)

      await doctorsApi.setPriority('t-1', true)

      expect(http.patch).toHaveBeenCalledWith('/queue/t-1/priority', { priority: true })
    })
  })

  describe('Doctor Profile & Schedules', () => {
    it('getOwnProfile lấy thông tin hồ sơ của chính bác sĩ đang đăng nhập', async () => {
      const profile = {
        id: 'doc-100',
        fullName: 'BS Nguyễn Văn Cường',
        specialty: 'Nội tổng quát',
        licenseNumber: 'BS-123456',
      }

      vi.mocked(http.get).mockResolvedValueOnce({
        data: { result: profile },
      } as any)

      const res = await doctorsApi.getOwnProfile()

      expect(http.get).toHaveBeenCalledWith('/doctors/me')
      expect(res.fullName).toBe('BS Nguyễn Văn Cường')
      expect(res.licenseNumber).toBe('BS-123456')
    })

    it('getOwnSchedule tải lịch trực của bác sĩ trong khoảng ngày', async () => {
      const mockSchedule = {
        doctorId: 'doc-100',
        schedules: [
          {
            id: 'ws-1',
            date: '2026-11-02',
            startTime: '08:00',
            endTime: '12:00',
            recommendedCapacity: 15,
          },
        ],
      }

      vi.mocked(http.get).mockResolvedValueOnce({
        data: { result: mockSchedule },
      } as any)

      const res = await doctorsApi.getOwnSchedule('2026-11-01', '2026-11-07')

      expect(http.get).toHaveBeenCalledWith('/shifts', {
        params: { StartDate: '2026-11-01', EndDate: '2026-11-07' },
      })
      expect(res.schedules).toHaveLength(1)
    })

    it('createShiftRequest gửi đề xuất ca trực mới', async () => {
      const requestPayload = {
        date: '2026-11-05',
        startTime: '08:00',
        endTime: '12:00',
        RecommendedCapacity: 20,
        reason: 'Đổi ca sang sáng',
      }

      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: { id: 'sug-1', ...requestPayload, status: 'Pending' } },
      } as any)

      const res = await doctorsApi.createShiftRequest(requestPayload)

      expect(http.post).toHaveBeenCalledWith('/shifts/suggestions', requestPayload)
      expect(res.id).toBe('sug-1')
    })

    it('cancelShiftRequest hủy đề xuất ca trực đã tạo', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: true },
      } as any)

      await doctorsApi.cancelShiftRequest('sug-1')

      expect(http.post).toHaveBeenCalledWith('/shifts/suggestions/sug-1/cancel')
    })
  })
})
