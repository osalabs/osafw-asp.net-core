$('.bi-caret-down').hide();

$(document).on('click', '.on-dropinfo', function (e) {
    var $this = $(this);
    var $alert = $this.closest('.dropinfo').find('.alert');
    if ( $alert.is(':visible') ){
        $alert.hide();
        $this.find('.bi-caret-up').hide();
        $this.find('.bi-caret-down').show();
    }else{
        $alert.show();
        $this.find('.bi-caret-up').show();
        $this.find('.bi-caret-down').hide();
    }
});