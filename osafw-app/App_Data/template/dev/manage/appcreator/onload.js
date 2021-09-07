$(document).on('click', '.on-all-cb', function (e) {
    var $this = $(this);
    var cbclass=$this.data('cb');
    $(cbclass).prop('checked', $this.prop('checked'));
});

$(document).on('change', '.cb-lookup', function (e) {
    var $this = $(this);
    var is_checked = $this.find('input').prop('checked');
    $this.closest('.col').find('.custom-checkbox:not(.cb-lookup)').find('input[type=checkbox]').prop('checked', !is_checked);
});