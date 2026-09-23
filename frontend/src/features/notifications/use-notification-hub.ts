import {
  HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel,
} from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { tokenStore } from '@/lib/api-client'
import type { AppNotification } from '@/lib/types'

const HUB_URL = import.meta.env.VITE_HUB_URL ?? '/hubs/notifications'

interface Options {
  enabled: boolean
  onNotification?: (notification: AppNotification) => void
}

/**
 * Kết nối SignalR tới notification-service.
 * Token đi qua accessTokenFactory: SignalR tự gắn vào query string cho WebSocket
 * và vào header cho long-polling.
 * Tự kết nối lại theo backoff khi rớt mạng.
 */
export function useNotificationHub({ enabled, onNotification }: Options) {
  const [connected, setConnected] = useState(false)
  const [unreadCount, setUnreadCount] = useState<number | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)
  const queryClient = useQueryClient()

  // Giữ callback trong ref để không phải dựng lại kết nối mỗi lần render.
  const handlerRef = useRef(onNotification)
  handlerRef.current = onNotification

  useEffect(() => {
    if (!enabled) return

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => tokenStore.access ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('ReceiveNotification', (notification: AppNotification) => {
      handlerRef.current?.(notification)
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'unread-count'] })

      // Thông báo đơn hàng thường kèm thay đổi tồn kho — làm mới luôn cho khớp.
      if (notification.metadata?.orderId) {
        void queryClient.invalidateQueries({ queryKey: ['orders'] })
        void queryClient.invalidateQueries({ queryKey: ['stocks'] })
      }
    })

    connection.on('UnreadCountChanged', (count: number) => setUnreadCount(count))

    connection.onreconnected(() => setConnected(true))
    connection.onreconnecting(() => setConnected(false))
    connection.onclose(() => setConnected(false))

    connection
      .start()
      .then(() => setConnected(true))
      .catch((error) => console.warn('Không kết nối được SignalR hub:', error))

    connectionRef.current = connection

    return () => {
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
      connectionRef.current = null
    }
  }, [enabled, queryClient])

  return { connected, unreadCount }
}
