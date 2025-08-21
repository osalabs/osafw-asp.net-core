// avoid sidebar flicker by applying collapsed state before styles load
(function () {
  const sidebarCollapsed = sessionStorage.getItem('sidebar-collapsed') === 'true';
  if (sidebarCollapsed && window.matchMedia('(min-width: 768px)').matches) {
    document.documentElement.classList.add('no-sidebar');
  }
})();
