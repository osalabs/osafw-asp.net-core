$(document).on('click', '.on-sidebar-toggler', function (e) {
  $('body').toggleClass('no-sidebar');
  $('#sidebar').removeClass('show');
});
