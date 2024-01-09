$(document).on('shown.bs.collapse', '#FActivityComment', function(e){
    $(this).find(':input:visible:first').focus();
});