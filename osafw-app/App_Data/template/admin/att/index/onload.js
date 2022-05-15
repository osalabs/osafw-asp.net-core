var $upload = $('#FQuickUpload');
$upload.find('input[type=file]').on('change', function(e){
    $upload.find('.msg-button').hide();
    $upload.find('.msg-uploading').removeClass('d-none');
    $upload.submit();
});