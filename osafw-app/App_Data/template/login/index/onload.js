$(document).on('click', '.on-pwd-hideshow', pwd_hideshow);
pwd_hideshow();
if (window.PublicKeyCredential) {
  $('#passkey-login-container').show();
}
$(document).on('click', '#passkey-login-btn', startPasskeyLogin);

$('#login').focus();

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

function startPasskeyLogin(){
  $.getJSON('<~/login/url>/(PasskeyStart)', function(options){
    options.publicKey.challenge = Uint8Array.from(atob(options.publicKey.challenge), c=>c.charCodeAt(0));
    options.publicKey.allowCredentials.forEach(function(c){ c.id = Uint8Array.from(atob(c.id), ch=>ch.charCodeAt(0)); });
    navigator.credentials.get({publicKey: options.publicKey}).then(function(cred){
      const authData={
        id: cred.id,
        rawId: btoa(String.fromCharCode.apply(null, new Uint8Array(cred.rawId))),
        type: cred.type,
        response:{
          authenticatorData: btoa(String.fromCharCode.apply(null, new Uint8Array(cred.response.authenticatorData))),
          clientDataJSON: btoa(String.fromCharCode.apply(null, new Uint8Array(cred.response.clientDataJSON))),
          signature: btoa(String.fromCharCode.apply(null, new Uint8Array(cred.response.signature))),
          userHandle: cred.response.userHandle?btoa(String.fromCharCode.apply(null,new Uint8Array(cred.response.userHandle))):null
        }
      };
      $.ajax({url:'<~/login/url>/(PasskeyLogin)',method:'POST',contentType:'application/json',data:JSON.stringify(authData)})
        .done(function(){window.location='<~/login/url>';})
        .fail(function(){alert('Passkey login failed');});
    }).catch(function(){alert('Passkey login cancelled');});
  });
}
