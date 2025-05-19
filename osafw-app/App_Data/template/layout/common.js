// Sidebar toggler/state
if (sessionStorage.getItem('sidebar-collapsed') === 'true') {
  $('body').addClass('no-sidebar');
} else {
  $('body').removeClass('no-sidebar');
}
$(document).on('click', '.on-sidebar-toggler', function (e) {
  e.preventDefault();
  $('body').toggleClass('no-sidebar');
  $('#sidebar').removeClass('show');
  sessionStorage.setItem('sidebar-collapsed', $('body').hasClass('no-sidebar'));
});
