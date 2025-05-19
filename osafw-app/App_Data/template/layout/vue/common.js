document.addEventListener('DOMContentLoaded', function () {
  // On page load, read the saved state and apply the class to the body.
  if (sessionStorage.getItem('sidebar-collapsed') === 'true') {
    document.body.classList.add('no-sidebar');
  } else {
    document.body.classList.remove('no-sidebar');
  }

  // Find all toggler buttons (works for Vue and non-Vue pages)
  var togglerButtons = document.querySelectorAll('.on-sidebar-toggler');
  togglerButtons.forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      // Toggle the sidebar (by toggling the no-sidebar class on <body>)
      document.body.classList.toggle('no-sidebar');

      // In case your sidebar element has its own classes (like Bootstrap’s .show),
      // remove them here.
      var sidebar = document.getElementById('sidebar');
      if (sidebar) {
        sidebar.classList.remove('show');
      }

      // Save the state in sessionStorage so that it persists during the session.
      var isCollapsed = document.body.classList.contains('no-sidebar');
      sessionStorage.setItem('sidebar-collapsed', isCollapsed);
    });
  });
});

