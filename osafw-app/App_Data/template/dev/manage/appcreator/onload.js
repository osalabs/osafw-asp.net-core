$(document).on('click', '.on-all-cb', function (e) {
    var $this = $(this);
    var cbclass=$this.data('cb');
    $(cbclass).prop('checked', $this.prop('checked'));
});

$(document).on('change', '.cb-colookup', function (e) {
    var $this = $(this);
    var is_checked = $this.prop('checked');
    console.log('colookup:', $this, $this.closest('.col').find('.cb-coview, .cb-coedit'));
    $this.closest('.col').find('.cb-coview, .cb-coedit').prop('checked', !is_checked);
});

$(document).on('keyup', '.on-filter', on_filter);
function on_filter(e){
    var $fs=$('#fs');
    var value = $fs.val();
    var $rows = $('.one-row');
    if (value>''){
        $rows.hide();
        $rows.filter('[data-s*="'+value+'"]').show();
    }else{
        $rows.show();
    }
}