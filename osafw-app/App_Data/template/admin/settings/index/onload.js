$('form[data-autosave]').on('submit', function(e) {
    e.preventDefault();
    e.stopPropagation();
    $(this).data('is-submitting', false).data('is-changed-submit', false);
    if ($(this).data('is-changed'))
        $(this).trigger('autosave');
});
