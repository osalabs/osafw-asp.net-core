var id = parseInt('<~id>',10);

$(document).on('click', '.on-send-pwd', on_send_pwd);
$(document).on('click', '.on-change-pwd', on_change_pwd);

if (!id){
    //workaround for Chrome autofill
    setTimeout(function (e) {
        $('#pwd').removeClass('d-none');
    },200);
}

function on_send_pwd(e) {
    var $this=$(this);
    $this.html('<span class="spinner-border spinner-border-sm"></span> ' + $this.html() );
    $.getJSON('<~../url>/(SendPwd)/<~id>', function(data){
        $this.find('.spinner-border').remove();
        if (data.success){
            fw.ok('Password reminder email sent');
        }else{
            fw.error('Server error occured: '+data.err_msg, {'sticky': true});
        }
    });
}

function on_change_pwd (e) {
    $(this).hide();
    $('#pwd').removeClass('d-none').focus();
}

$('#pwd').on('blur change keyup', fw.renderPwdBar);