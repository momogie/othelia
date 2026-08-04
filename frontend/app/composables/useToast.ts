export interface ToastState {
  show: boolean
  msg: string
  icon: string
  type: 'success' | 'error' | 'info'
}

export function useToast() {
  const toast = useState<ToastState>('toast', () => ({ show: false, msg: '', icon: '', type: 'success' }))
  let timer: ReturnType<typeof setTimeout> | undefined

  function show(msg: string, icon = '✅', type: ToastState['type'] = 'success') {
    if (timer) clearTimeout(timer)
    toast.value = { show: true, msg, icon, type }
    timer = setTimeout(() => {
      toast.value = { ...toast.value, show: false }
    }, 3000)
  }

  function close() {
    if (timer) clearTimeout(timer)
    toast.value = { ...toast.value, show: false }
  }

  return { toast, show, close }
}
