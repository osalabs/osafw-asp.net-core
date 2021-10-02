$(document).on('click', '.on-pwd-hideshow', pwd_hideshow);
pwd_hideshow();

$('#login').focus()

function pwd_hideshow(){
  var chpwd;
  if ( $('#chpwd')[0].checked ){
    $('#pwdh').hide();
    $('#pwd').show().val( $('#pwdh').val() ).trigger('change');
  }else{
    $('#pwd').hide();
    $('#pwdh').show().val( $('#pwd').val() ).trigger('change');
  }
}
