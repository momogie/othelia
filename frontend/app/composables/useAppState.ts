export function useAppState() {
  const sidebarCollapsed = useState<boolean>('sidebarCollapsed', () => false)

  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
  }

  return { sidebarCollapsed, toggleSidebar }
}
