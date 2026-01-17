var $scope = $(fw.scopeFromScript());
var $modal = $scope.closest('.modal');
if (!$modal.length) $modal = $('.fw-modal.show').first();
if (!$modal.length) $modal = $('.fw-modal').last();

var trigger = fw.modalTriggerEl($scope);
var $trigger = $(trigger);

function getTrigger() {
    if ($trigger && $trigger.length) return $trigger;
    if ($modal.length) {
        var triggerId = $modal.data('fw-modal-trigger-id');
        if (triggerId) {
            $trigger = $('[data-fw-modal-trigger-id="' + triggerId + '"]');
        }
    }
    return $trigger;
}

function getFieldGroup() {
    var $activeTrigger = getTrigger();
    var $fg = $activeTrigger.closest('.fw-att-block');
    if (!$fg.length) $fg = $activeTrigger.closest('.form-row, .form-group');
    return $fg;
}

function applySelection(id, iname, url, is_image) {
    var $fg = getFieldGroup();
    if (!$fg.length) return;
    var $activeTrigger = getTrigger();
    var $form = $activeTrigger.closest('form');
    var $att_list = $fg.find('.att-list');
    var att_preview_size = $activeTrigger.data('att-preview-size') || 's';
    var $attinfo;

    if ($att_list.length){//multi att
        $attinfo = $att_list.find('.tpl').clone().removeClass('tpl d-none').appendTo($att_list);
        $attinfo.find(':input:hidden').prop('name', 'att['+id+']');
    }else{
        $attinfo = $fg.find('.att-info').show();
        $attinfo.find(':input:hidden').val(id);
    }

    $attinfo.find('img').show().prop('src', url+'?preview=1&size='+att_preview_size);
    $attinfo.find('a').prop('href', '/Att/'+id);
    $attinfo.find('.att-iname').text(iname);
    fw.update_att_empty_state($fg);
    $form.trigger('autosave');
}

function replaceModalContent(html) {
    var $content = $modal.find('.modal-content');
    $content.html(html);
    $content.find('script').each(function () {
        var oldScript = this;
        var newScript = document.createElement('script');

        Array.from(oldScript.attributes).forEach(function (attr) {
            newScript.setAttribute(attr.name, attr.value);
        });

        if (oldScript.src) {
            newScript.src = oldScript.src;
            newScript.async = false;
        } else {
            newScript.text = oldScript.text;
        }

        oldScript.parentNode.replaceChild(newScript, oldScript);
    });
}

$modal.off('click', '.thumbs a').on('click', '.thumbs a', function (e) {
    e.preventDefault();

    var $img = $(this).find('img');
    applySelection($(this).data('id'), $img.prop('alt'), $img.data('url'), $img.data('is_image'));

    $modal.modal('hide');
    return false;
});

//refresh if category changed
$modal.find('select[name="item[att_categories_id]"]').on('change', function(e){
    var $activeTrigger = getTrigger();
    var baseUrl = $activeTrigger.data('base-url');
    if (!baseUrl) {
        var currentUrl = $modal.data('fw-modal-url') || $activeTrigger.data('url') || $activeTrigger.attr('href');
        if (currentUrl) baseUrl = currentUrl.split('?')[0];
    }
    if (!baseUrl) return;
    var url = baseUrl + '?att_categories_id=' + encodeURIComponent($(this).val());
    $modal.find('.modal-content').find('.thumbs').html(fw.HTML_LOADING);
    $.ajax({
        url: url,
        method: 'GET',
        dataType: 'html'
    })
        .done(function (html) {
            replaceModalContent(html);
        })
        .fail(function (xhr) {
            var message = xhr.responseText || 'Failed to load modal content.';
            replaceModalContent('<div class="modal-body"><div class="alert alert-danger mb-0">' + message + '</div></div>');
        });
});

$modal.find('input[type=file]').on('change', function(e){
    $modal.find('.msg-button').hide();
    $modal.find('.msg-uploading').removeClass('d-none');
    $modal.find('form').submit();
});

$modal.find('[data-bs-dismiss=modal]').on('click', function (e) {
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
        if (!data.error){
            applySelection(data.id, data.iname, data.url, data.is_image);
            $modal.modal('hide');
        }else{
            fw.error(data.error?.message??'Server error');
        }
    }
});
