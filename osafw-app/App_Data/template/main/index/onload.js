$(document).on('click', '.on-card-click', function (e) {
    if ($(e.target).closest('a, button, input, textarea, select, label, form').length) return;
    window.location=$(this).data('url');
});
