import { describe, it, expect, vi, beforeEach } from 'vitest'
import { buildGatewayHubWebSocketUrl, notificationsApi } from '../notifications.api'
import { http } from '@/lib/api/http'
import { tokenStorage } from '@/lib/api/token-storage'

vi.mock('@/lib/api/http', () => ({
  http: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
  },
}))

vi.mock('@/lib/api/token-storage', () => ({
  tokenStorage: {
    getAccess: vi.fn(),
  },
}))

describe('notificationsApi (Gateway REST + SignalR hint client)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('builds Gateway WebSocket URL without targeting Notification Service directly', () => {
    const wsUrl = buildGatewayHubWebSocketUrl('/api', 'jwt-token-123', 'conn-abc', {
      protocol: 'http:',
      host: 'localhost:5173',
    })
    expect(wsUrl).toBe('ws://localhost:5173/api/hubs/notifications?id=conn-abc&access_token=jwt-token-123')
    expect(wsUrl).not.toContain('5006')
  })

  it('fetches paginated history and unread count via Gateway REST endpoints', async () => {
    vi.mocked(http.get)
      .mockResolvedValueOnce({
        data: {
          result: {
            items: [
              {
                id: 'n-1',
                category: 'AppointmentConfirmed',
                title: 'Appointment Confirmed',
                content: 'Confirmed',
                isRead: false,
                createdAt: '2026-10-30T00:00:00Z',
              },
            ],
            pageNumber: 1,
            pageSize: 10,
            totalCount: 1,
          },
        },
      } as any)
      .mockResolvedValueOnce({
        data: { result: { count: 3 } },
      } as any)

    const page = await notificationsApi.list({ pageNumber: 1, pageSize: 10 })
    const unread = await notificationsApi.getUnreadCount()

    expect(http.get).toHaveBeenCalledWith('/me/notifications', { params: { pageNumber: 1, pageSize: 10 } })
    expect(http.get).toHaveBeenCalledWith('/notifications/unread-count')
    expect(page.totalCount).toBe(1)
    expect(page.items[0]?.id).toBe('n-1')
    expect(unread).toBe(3)
  })

  it('treats SignalR NotificationHint frame as a hint to resync from REST history', async () => {
    vi.mocked(tokenStorage.getAccess).mockReturnValue('jwt-user-a')
    vi.mocked(http.post).mockResolvedValue({
      data: { connectionToken: 'neg-token-1', connectionId: 'cid-1' },
    } as any)

    let createdSocket: any = null
    const sentFrames: string[] = []
    const onSyncRequired = vi.fn()

    const handle = notificationsApi.connectRealtimeHints({
      onSyncRequired,
      webSocketFactory: (url: string) => {
        createdSocket = {
          url,
          readyState: 1,
          send: (data: string) => sentFrames.push(data),
          close: vi.fn(),
          onopen: null as any,
          onmessage: null as any,
          onclose: null as any,
          onerror: null as any,
        }
        return createdSocket as unknown as WebSocket
      },
    })

    // Wait for negotiate promise microtask
    await new Promise((resolve) => setTimeout(resolve, 10))

    expect(createdSocket).not.toBeNull()
    expect(createdSocket.url).toContain('/api/hubs/notifications?id=neg-token-1&access_token=jwt-user-a')

    createdSocket.onopen?.()
    expect(sentFrames[0]).toBe('{"protocol":"json","version":1}\x1e')

    createdSocket.onmessage?.({
      data: '{"type":1,"target":"NotificationHint","arguments":[{"id":"notif-99","title":"Your Turn"}]}\x1e',
    })

    expect(onSyncRequired).toHaveBeenCalledTimes(1)
    expect(onSyncRequired).toHaveBeenCalledWith({ id: 'notif-99', title: 'Your Turn' })

    handle.stop()
  })
})
