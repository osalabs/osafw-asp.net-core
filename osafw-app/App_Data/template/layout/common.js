$(document).on('click', '.on-sidebar-toggler', function (e) {
  $('body').toggleClass('no-sidebar');
  $('#sidebar').removeClass('show');
});

function getPrefUIMode() {
  let mode = document.documentElement.getAttribute('data-ui-mode');
  if (mode) return mode;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
function setUIMode(mode) {
  if (mode === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches) {
    document.documentElement.setAttribute('data-bs-theme', 'dark');
  } else {
    document.documentElement.setAttribute('data-bs-theme', mode);
  }
}
//setUIMode(getPrefUIMode());
