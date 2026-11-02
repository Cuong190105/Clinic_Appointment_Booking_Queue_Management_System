import { env } from '@/config/env'
import { tokenStorage } from '@/lib/api/token-storage'

export interface RealtimeNotification {
  id: string
  title: string
  content: string
  createdAt: string
}

const recordSeparator = '\u001e'
let socket: WebSocket | null = null
let reconnectTimer: number | null = null
let stopped = true
let receiver: ((message: RealtimeNotification) => void) | null = null

export async function connectNotifications(onReceived: (message: RealtimeNotification) => void): Promise<void> {
  const token = tokenStorage.getAccess()
  if (!token || socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return
  stopped = false
  receiver = onReceived
  const hubPath = `${env.apiBaseUrl.replace(/\/$/, '')}/hubs/notifications`
  try {
    const negotiate = await fetch(`${hubPath}/negotiate?negotiateVersion=1`, {
      method: 'POST',
      credentials: 'include',
      headers: { Authorization: `Bearer ${token}` },
    })
    if (!negotiate.ok) return
    const details = await negotiate.json() as { connectionToken?: string }
    if (!details.connectionToken) return
    const url = new URL(hubPath, window.location.origin)
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'
    url.searchParams.set('id', details.connectionToken)
    url.searchParams.set('access_token', token)
    socket = new WebSocket(url)
    socket.onopen = () => socket?.send(`${JSON.stringify({ protocol: 'json', version: 1 })}${recordSeparator}`)
    socket.onmessage = (event) => {
      for (const frame of String(event.data).split(recordSeparator).filter(Boolean)) {
        const message = JSON.parse(frame) as { type?: number; target?: string; arguments?: RealtimeNotification[] }
        if (message.type === 1 && message.target === 'NotificationReceived' && message.arguments?.[0]) receiver?.(message.arguments[0])
      }
    }
    socket.onclose = () => {
      socket = null
      if (!stopped) reconnectTimer = window.setTimeout(() => receiver && connectNotifications(receiver), 5_000)
    }
  } catch {
    if (!stopped) reconnectTimer = window.setTimeout(() => receiver && connectNotifications(receiver), 5_000)
  }
}

export async function disconnectNotifications(): Promise<void> {
  stopped = true
  receiver = null
  if (reconnectTimer !== null) window.clearTimeout(reconnectTimer)
  reconnectTimer = null
  socket?.close()
  socket = null
}
