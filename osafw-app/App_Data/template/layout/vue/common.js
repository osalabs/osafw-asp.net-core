document.addEventListener('DOMContentLoaded', function () {
  const sidebarCollapsed = sessionStorage.getItem('sidebar-collapsed') === 'true';
  if (sidebarCollapsed && window.matchMedia('(min-width: 768px)').matches) {
    document.body.classList.add('no-sidebar');
  }

  // Find all toggler buttons (works for Vue and non-Vue pages)
  var togglerButtons = document.querySelectorAll('.on-sidebar-toggler');
  togglerButtons.forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      // let Bootstrap collapse handle show/hide
      if (window.matchMedia('(min-width: 768px)').matches) {
        document.body.classList.toggle('no-sidebar');
        var isCollapsed = document.body.classList.contains('no-sidebar');
        sessionStorage.setItem('sidebar-collapsed', isCollapsed);
      }
    });
  });
});

