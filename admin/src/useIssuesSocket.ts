import { useEffect, useRef } from 'react'
import { apiBase } from './api.ts'
import {
  parseWsPayload,
  wsUrlFromHttp,
  nextBackoff,
  WS_BACKOFF_START_MS,
  type WsEvent,
} from './issuesWs.ts'

export function useIssuesSocket(
  enabled: boolean,
  token: string | null,
  onEvent: (event: WsEvent) => void,
): void {
  const onEventRef = useRef(onEvent)
  onEventRef.current = onEvent

  useEffect(() => {
    if (!enabled || !token) return

    let closed = false
    let socket: WebSocket | null = null
    let retryTimer: ReturnType<typeof setTimeout> | undefined
    let delay = WS_BACKOFF_START_MS

    function connect() {
      if (closed) return
      const ws = new WebSocket(wsUrlFromHttp(apiBase(), token))
      socket = ws
      ws.onopen = () => {
        delay = WS_BACKOFF_START_MS
      }
      ws.onmessage = (ev) => {
        const event = parseWsPayload(String(ev.data))
        if (event) onEventRef.current(event)
      }
      ws.onclose = () => {
        if (closed) return
        retryTimer = setTimeout(() => {
          const wait = delay
          delay = nextBackoff(wait)
          connect()
        }, delay)
      }
    }

    connect()
    return () => {
      closed = true
      if (retryTimer) clearTimeout(retryTimer)
      socket?.close()
    }
  }, [enabled, token])
}
