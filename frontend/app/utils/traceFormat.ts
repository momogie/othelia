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
  const s = dur.trim()
  const m = /^([\d.]+)\s*(ms|s|min)$/.exec(s)
  if (!m) return dur
  const val = parseFloat(m[1])
  const halfMs = m[2] === 'ms' ? val / 2 : m[2] === 's' ? (val * 1000) / 2 : (val * 60000) / 2
  if (halfMs < 1000) return `${Math.round(halfMs)}ms`
  if (halfMs < 60000) return `${(halfMs / 1000).toFixed(2)}s`
  return `${(halfMs / 60000).toFixed(1)}min`
}
