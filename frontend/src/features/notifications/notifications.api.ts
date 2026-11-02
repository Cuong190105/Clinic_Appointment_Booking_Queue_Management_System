import { http } from '@/lib/api/http'
import { env } from '@/config/env'
import { tokenStorage } from '@/lib/api/token-storage'

export interface PaginatedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
}

export interface NotificationItemDto {
  id: string
  category: string | number
  title: string
  content: string
  isRead: boolean
  createdAt: string
  readAt?: string | null
}

export interface NotificationHintPayload {
  id?: string
  category?: string | number
  title?: string
  createdAt?: string
}

const SIGNALR_RECORD_SEPARATOR = '\x1e'

function unwrapEnvelope<T>(raw: any): T {
  if (raw && typeof raw === 'object' && 'result' in raw && raw.result !== undefined) {
    return raw.result as T
  }
  return raw as T
}

export function buildGatewayHubWebSocketUrl(
  apiBaseUrl: string,
  accessToken: string,
  connectionToken?: string,
  locationObj: Pick<Location, 'protocol' | 'host'> = window.location,
): string {
  const base = apiBaseUrl.replace(/\/+$/, '')
  let wsRoot: string
  if (/^https?:\/\//i.test(base)) {
    wsRoot = base.replace(/^http/i, 'ws')
  } else {
    const wsProto = locationObj.protocol === 'https:' ? 'wss:' : 'ws:'
    const normalizedPath = base.startsWith('/') ? base : `/${base}`
    wsRoot = `${wsProto}//${locationObj.host}${normalizedPath}`
  }

  const params = new URLSearchParams()
  if (connectionToken) {
    params.set('id', connectionToken)
  }
  params.set('access_token', accessToken)
  return `${wsRoot}/hubs/notifications?${params.toString()}`
}

export const notificationsApi = {
  async list(params: { pageNumber?: number; pageSize?: number } = {}): Promise<PaginatedResult<NotificationItemDto>> {
    const pageNumber = params.pageNumber ?? 1
    const pageSize = params.pageSize ?? 20
    const res = await http
      .get<any>('/me/notifications', { params: { pageNumber, pageSize } })
      .then((r) => unwrapEnvelope<any>(r.data))

    return {
      items: res?.items ?? res?.Items ?? [],
      pageNumber: res?.pageNumber ?? res?.PageNumber ?? pageNumber,
      pageSize: res?.pageSize ?? res?.PageSize ?? pageSize,
      totalCount: res?.totalCount ?? res?.TotalCount ?? 0,
    }
  },

  async getUnreadCount(): Promise<number> {
    const res = await http
      .get<any>('/notifications/unread-count')
      .then((r) => unwrapEnvelope<{ count?: number; Count?: number }>(r.data))
    return res?.count ?? res?.Count ?? 0
  },

  async markAsRead(id: string): Promise<void> {
    await http.put(`/notifications/${encodeURIComponent(id)}/read`)
  },

  async markAllAsRead(): Promise<void> {
    await http.put('/notifications/read-all')
  },

  async negotiate(): Promise<{ connectionToken?: string; connectionId?: string }> {
    const res = await http
      .post<any>('/hubs/notifications/negotiate?negotiateVersion=1')
      .then((r) => unwrapEnvelope<any>(r.data))
    return {
      connectionToken: res?.connectionToken ?? res?.connectionId,
      connectionId: res?.connectionId,
    }
  },

  /**
   * Connects to the Gateway SignalR Hub `/hubs/notifications`.
   * Real-time messages are treated strictly as a hint (`NotificationHint`), triggering
   * `onSyncRequired` so the caller reloads canonical state from Gateway REST history.
   */
  connectRealtimeHints(options: {
    onSyncRequired: (hint?: NotificationHintPayload) => void | Promise<void>
    webSocketFactory?: (url: string) => WebSocket
    reconnectDelayMs?: number
  }): { stop: () => void } {
    let disposed = false
    let socket: WebSocket | null = null
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null
    const reconnectDelayMs = options.reconnectDelayMs ?? 5000

    const scheduleReconnect = () => {
      if (disposed || reconnectTimer) return
      reconnectTimer = setTimeout(() => {
        reconnectTimer = null
        void startConnection(true)
      }, reconnectDelayMs)
    }

    const startConnection = async (isReconnect: boolean) => {
      if (disposed) return
      const token = tokenStorage.getAccess()
      if (!token) return

      let connectionToken: string | undefined
      try {
        const neg = await notificationsApi.negotiate()
        connectionToken = neg.connectionToken
      } catch {
        // Proceed with direct WebSocket upgrade via Gateway if negotiate fails or is skipped
      }

      if (disposed) return

      const wsUrl = buildGatewayHubWebSocketUrl(env.apiBaseUrl, token, connectionToken)
      const createWs = options.webSocketFactory ?? ((url: string) => new WebSocket(url))

      try {
        socket = createWs(wsUrl)
      } catch {
        scheduleReconnect()
        return
      }

      socket.onopen = () => {
        if (disposed || !socket) return
        // SignalR JSON protocol handshake
        socket.send(`{"protocol":"json","version":1}${SIGNALR_RECORD_SEPARATOR}`)
        if (isReconnect) {
          // Always resynchronize via REST history after reconnect
          void options.onSyncRequired()
        }
      }

      socket.onmessage = (event: MessageEvent) => {
        if (disposed || typeof event.data !== 'string') return
        const frames = event.data.split(SIGNALR_RECORD_SEPARATOR).filter(Boolean)
        for (const frame of frames) {
          try {
            const msg = JSON.parse(frame)
            if (msg.type === 1 && (msg.target === 'NotificationHint' || msg.target === 'ReceiveNotification')) {
              const hint = Array.isArray(msg.arguments) ? (msg.arguments[0] as NotificationHintPayload) : undefined
              void options.onSyncRequired(hint)
            } else if (msg.type === 6 && socket?.readyState === WebSocket.OPEN) {
              socket.send(`{"type":6}${SIGNALR_RECORD_SEPARATOR}`)
            }
          } catch {
            // Ignore malformed frame
          }
        }
      }

      socket.onclose = () => {
        socket = null
        if (!disposed) {
          scheduleReconnect()
        }
      }

      socket.onerror = () => {
        // onclose will handle reconnect
      }
    }

    void startConnection(false)

    return {
      stop: () => {
        disposed = true
        if (reconnectTimer) {
          clearTimeout(reconnectTimer)
          reconnectTimer = null
        }
        if (socket) {
          try {
            socket.close()
          } catch {
            // Ignore close errors
          }
          socket = null
        }
      },
    }
  },
}
