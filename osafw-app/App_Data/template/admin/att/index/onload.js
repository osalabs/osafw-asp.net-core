var $upload = $('#FQuickUpload');
$upload.find('input[type=file]').on('change', function(e){
    $upload.find('.wait-msg').html(fw.HTML_LOADING);    
    $upload.submit();
});