import type { TraceStatus } from './mockData'

export function statusColor(status: TraceStatus) {
  if (status === 'error') return 'var(--red)'
  if (status === 'slow') return 'var(--yellow)'
  return 'var(--green)'
}

export function statusCodeClass(code: number) {
  if (code >= 500) return 'sc-5xx'
  if (code >= 400) return 'sc-4xx'
  return 'sc-2xx'
}

export function halfDur(dur: string) {
  const n = parseInt(dur, 10)
  if (dur.includes('s') && !dur.includes('ms')) return `${Math.round(n * 500)}ms`
  return `${Math.round(n / 2)}ms`
}
