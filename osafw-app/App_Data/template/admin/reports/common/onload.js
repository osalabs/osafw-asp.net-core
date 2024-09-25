$(document).on('click', '.on-print', function(e){
 window.print();
});

// Send to Email
function refresh_emailto(){
  var $emailto = $('#emailto');
  var $f = $emailto.closest('form');
  if ($('#emailto').is(':visible')){
    $('#to_emails').prop('required', true).focus();
    $f[0].action=$f.data('url-email');
    $f[0].method='post';
  }else{
    $('#to_emails').prop('required', false);
    $f[0].action='';
    $f[0].method='get';
  }
}
//
$(document).on('click', '.on-email', function(e){
  $('#emailto').toggle();
  refresh_emailto();
});

$(document).on('click', '.on-email-cancel', function(e){
  $('#emailto').hide();
});
