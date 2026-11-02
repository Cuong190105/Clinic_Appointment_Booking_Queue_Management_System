import { describe, it, expect, vi, beforeEach } from 'vitest'
import { receptionistApi } from '../receptionist.api'
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

describe('receptionistApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('searchPatients', () => {
    it('sends correct query parameters and unwrap pagination items', async () => {
      vi.mocked(http.get).mockResolvedValueOnce({
        data: {
          result: {
            items: [{ id: 'p-1', fullName: 'Nguyen Van A', phoneNumber: '0901234567' }],
            totalCount: 1,
            pageNumber: 1,
            pageSize: 20,
          },
        },
      } as any)

      const result = await receptionistApi.searchPatients('Nguyen', 1, 20)

      expect(http.get).toHaveBeenCalledWith('/patients', {
        params: {
          Search: 'Nguyen',
          pageNumber: 1,
          pageSize: 20,
        },
      })
      expect(result.items).toHaveLength(1)
      expect(result.items?.[0]?.fullName).toBe('Nguyen Van A')
      expect(result.totalCount).toBe(1)
    })

    it('handles bare array response gracefully', async () => {
      vi.mocked(http.get).mockResolvedValueOnce({
        data: [{ id: 'p-2', fullName: 'Tran Thi B' }],
      } as any)

      const result = await receptionistApi.searchPatients('Tran')
      expect(result.items).toHaveLength(1)
      expect(result.totalCount).toBe(1)
    })
  })

  describe('createWalkInAppointment', () => {
    it('passes Idempotency-Key header and returns unwrapped result', async () => {
      const payload = {
        patientId: 'p-1',
        specialtyId: 'sp-1',
        doctorId: 'd-1',
        reason: 'Đau đầu',
      }
      const mockResult = {
        appointmentId: 'apt-1',
        queueNumber: 5,
        doctorName: 'BS Nam',
      }

      vi.mocked(http.post).mockResolvedValueOnce({
        data: { result: mockResult },
      } as any)

      const result = await receptionistApi.createWalkInAppointment(payload, 'idemp-key-123')

      expect(http.post).toHaveBeenCalledWith('/receptionist/appointments/walk-in', payload, {
        headers: { 'Idempotency-Key': 'idemp-key-123' },
      })
      expect(result).toEqual(mockResult)
    })
  })

  describe('checkInAppointment', () => {
    it('sends isUrgent param and generates UUID Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: {
          result: {
            appointmentId: 'apt-10',
            queueNumber: 'A-003',
            status: 'Waiting',
          },
        },
      } as any)

      const result = await receptionistApi.checkInAppointment('apt-10', true)

      expect(http.post).toHaveBeenCalledWith(
        '/appointments/apt-10/check-in',
        undefined,
        expect.objectContaining({
          params: { isUrgent: true },
          headers: expect.objectContaining({
            'Idempotency-Key': expect.any(String),
          }),
        }),
      )
      expect(result.queueNumber).toBe('A-003')
    })
  })

  describe('getUpcomingAppointments', () => {
    it('returns appointments from primary endpoint if successful', async () => {
      const items = [{ id: 'apt-1', patientName: 'Nguyen Van A' }]
      vi.mocked(http.get).mockResolvedValueOnce({
        data: { result: items },
      } as any)

      const res = await receptionistApi.getUpcomingAppointments('p-1')
      expect(http.get).toHaveBeenCalledWith('/patients/p-1/appointments')
      expect(res).toEqual(items)
    })

    it('falls back to /upcoming endpoint if primary endpoint throws', async () => {
      const items = [{ id: 'apt-fallback', patientName: 'Nguyen Van A' }]
      vi.mocked(http.get)
        .mockRejectedValueOnce(new Error('404 Not Found'))
        .mockResolvedValueOnce({
          data: { result: items },
        } as any)

      const res = await receptionistApi.getUpcomingAppointments('p-1')
      expect(http.get).toHaveBeenCalledWith('/patients/p-1/appointments')
      expect(http.get).toHaveBeenCalledWith('/patients/p-1/appointments/upcoming')
      expect(res).toEqual(items)
    })

    it('returns empty array if both primary and fallback endpoints fail', async () => {
      vi.mocked(http.get)
        .mockRejectedValueOnce(new Error('404'))
        .mockRejectedValueOnce(new Error('500'))

      const res = await receptionistApi.getUpcomingAppointments('p-1')
      expect(res).toEqual([])
    })
  })

  describe('Queue actions (callTicket, skipTicket, retryTicket, setPriority)', () => {
    it('callTicket sends POST to /queue/:id/call', async () => {
      const ticket = { id: 't-1', status: 'Called', queueNumber: 1 }
      vi.mocked(http.post).mockResolvedValueOnce({ data: { result: ticket } } as any)

      const res = await receptionistApi.callTicket('t-1')
      expect(http.post).toHaveBeenCalledWith('/queue/t-1/call')
      expect(res).toEqual(ticket)
    })

    it('skipTicket sends POST to /queue/:id/skip', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({ data: null } as any)

      await receptionistApi.skipTicket('t-1')
      expect(http.post).toHaveBeenCalledWith('/queue/t-1/skip')
    })

    it('retryTicket sends POST to /queue/:id/retry', async () => {
      const ticket = { id: 't-1', status: 'Waiting', queueNumber: 1 }
      vi.mocked(http.post).mockResolvedValueOnce({ data: { result: ticket } } as any)

      const res = await receptionistApi.retryTicket('t-1')
      expect(http.post).toHaveBeenCalledWith('/queue/t-1/retry')
      expect(res).toEqual(ticket)
    })

    it('setPriority sends PATCH to /queue/:id/priority', async () => {
      vi.mocked(http.patch).mockResolvedValueOnce({ data: null } as any)

      await receptionistApi.setPriority('t-1', true)
      expect(http.patch).toHaveBeenCalledWith('/queue/t-1/priority', { priority: true })
    })
  })

  describe('pollQueueTicketByAppointmentId', () => {
    it('resolves immediately when ticket has queueNumber', async () => {
      const ticket = { id: 't-1', queueNumber: 3, status: 'Waiting' }
      vi.mocked(http.get).mockResolvedValueOnce({ data: { result: ticket } } as any)

      const res = await receptionistApi.pollQueueTicketByAppointmentId('apt-1', 5, 10)
      expect(res).toEqual(ticket)
      expect(http.get).toHaveBeenCalledTimes(1)
    })

    it('retries on 404 / 503 transient errors and returns ticket when ready', async () => {
      const ticket = { id: 't-2', queueNumber: 7, status: 'Waiting' }
      const err404 = { response: { status: 404 } }
      const err503 = { response: { status: 503 } }

      vi.mocked(http.get)
        .mockRejectedValueOnce(err404)
        .mockRejectedValueOnce(err503)
        .mockResolvedValueOnce({ data: { result: ticket } } as any)

      const res = await receptionistApi.pollQueueTicketByAppointmentId('apt-2', 5, 5)
      expect(res).toEqual(ticket)
      expect(http.get).toHaveBeenCalledTimes(3)
    })

    it('returns null when maxRetries is reached without obtaining ticket', async () => {
      vi.mocked(http.get).mockRejectedValue({ response: { status: 404 } })

      const res = await receptionistApi.pollQueueTicketByAppointmentId('apt-3', 3, 5)
      expect(res).toBeNull()
      expect(http.get).toHaveBeenCalledTimes(3)
    })

    it('re-throws immediately on non-transient error (e.g. 400 or 401)', async () => {
      const err400 = { response: { status: 400, data: { message: 'Bad request' } } }
      vi.mocked(http.get).mockRejectedValueOnce(err400)

      await expect(
        receptionistApi.pollQueueTicketByAppointmentId('apt-4', 5, 5),
      ).rejects.toEqual(err400)
      expect(http.get).toHaveBeenCalledTimes(1)
    })
  })
})
