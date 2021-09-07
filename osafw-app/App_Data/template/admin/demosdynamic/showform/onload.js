$('form[data-autosave]').on('autosave-success', function(e, data) {
    console.log('autosaved', data);
});
