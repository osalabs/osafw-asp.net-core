var $topnav = $('#topnav-row .col');
var $sidebar = $('#main-row .sidebar');
var $main  = $('#main-row .main');
$(document).on('click', '.on-sidebar-toggler', function (e) {
    $sidebar.toggleClass('col-md-3 col-lg-2 d-md-block col-12');
    $topnav.toggleClass('d-md-none');

    $main.toggleClass('col-md-9 col-lg-10 col-12');
});
