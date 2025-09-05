$(document).on('click', '.on-pwd-hideshow', pwd_hideshow);
pwd_hideshow();

$('#login').focus();
$('#timezone').val(Intl.DateTimeFormat().resolvedOptions().timeZone);

function pwd_hideshow(){
  var $chpwd=$('#chpwd');
  if (!$chpwd.length){
     return;
  }

  if ( $chpwd[0].checked ){
    $('#pwdh').hide();
    $('#pwd').show().val( $('#pwdh').val() ).trigger('change');
  }else{
    $('#pwd').hide();
    $('#pwdh').show().val( $('#pwd').val() ).trigger('change');
  }
}
