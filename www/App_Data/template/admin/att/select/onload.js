var $modal = $('#modal-att');

$modal.off('click', '.thumbs a').on('click', '.thumbs a', function (e) {
    e.preventDefault();

    var $img = $(this).find('img');
    $modal.trigger('select.modal-att', [$(this).data('id'), $img.prop('alt'), $img.data('url'), $img.data('is_image') ] );

    $modal.modal('hide');
    return false;
});

//refresh if category changed
$modal.find('select[name="item[att_categories_id]"]').on('change', function(e){
    var url = $modal.data('load-url')+'?att_categories_id='+$(this).val();
    $modal.find('.modal-content').find('.thumbs').html(fw.HTML_LOADING).end().load( url );
});

$modal.find('input[type=file]').on('change', function(e){
    $modal.find('.msg-button').hide();
    $modal.find('.msg-uploading').removeClass('d-none');
    $modal.find('form').submit();
});

$modal.find('[data-dismiss=modal]').on('click', function (e) {
    $modal.modal('hide');
});

$modal.find('form').ajaxForm({
    dataType : 'json',
    beforeSubmit: function(arr, $form, options) {
        if ( !$form.find("input[type=file]").val() ){
            fw.error("Please select file first!");
            return false;
        }
    // The array of form data takes the following form:
    // [ { name: 'username', value: 'jresig' }, { name: 'password', value: 'secret' } ]

    // return false to cancel submit
    },
    success  : function (data) {
        if (data.success){
            $modal.trigger('select.modal-att', [data.id, data.iname, data.url, data.is_image ] );
            $modal.modal('hide');
        }else{
            fw.error(data.err_msg);
        }
    }
});