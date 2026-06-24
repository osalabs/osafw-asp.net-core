var id = parseInt('<~id>',10);

$(document).on('click', '.on-change-pwd', on_change_pwd);

if (!id){
    //workaround for Chrome autofill
    setTimeout(function (e) {
        $('#pwd').removeClass('d-none');
    },200);
}

function on_change_pwd (e) {
    $(this).hide();
    $('#pwd').removeClass('d-none').focus();
}

$('#pwd').on('blur change keyup', fw.renderPwdBar);
