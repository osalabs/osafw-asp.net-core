// Sidebar toggler/state
const sidebarCollapsed = sessionStorage.getItem('sidebar-collapsed') === 'true';
if (sidebarCollapsed && window.matchMedia('(min-width: 768px)').matches) {
  $('body').addClass('no-sidebar');
}

$(document).on('click', '.on-sidebar-toggler', function (e) {
  // allow default behavior so Bootstrap collapse works
  if (window.matchMedia('(min-width: 768px)').matches) {
    $('body').toggleClass('no-sidebar');
    sessionStorage.setItem('sidebar-collapsed', $('body').hasClass('no-sidebar'));
  }
});
