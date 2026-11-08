$(document).on('click', '.on-print', function(e){
 window.print();
});

// Send to Email
$(document).on('click', '.on-email', function(e){
  if ($('#emailto').toggle().is(':visible'))
    $('#to_emails').trigger('focus');
});

$(document).on('click', '.on-email-cancel', function(e){
  $('#emailto').hide();
});

$(document).on('click', '.on-email-send', function(e){
  const recipient = this.form.querySelector('#to_emails');
  recipient.required = true;
  const is_valid = recipient.reportValidity();
  recipient.required = false;
  if (!is_valid)
    e.preventDefault();
});

// Keep draft email fields out of filter and download URLs.
document.addEventListener('formdata', function(e){
  if (e.target.querySelector('#emailto') && !e.formData.has('send_email')){
    e.formData.delete('f[to_emails]');
    e.formData.delete('f[email_as]');
  }
}, true);
