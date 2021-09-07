var mode = '<~f[mode]>';

$(document).on('click', '.on-mode-switch', function(e){
  var value = $(e.target).find('input').val();
  $('#FFilter').find('input[name="f[mode]"]').val( value );
  $('#FFilter').submit();
});

if (mode){
    //table edit mode
    var $tedit = $('.table-edit');
    $tedit.data('newid',0);

    $tedit.on('click', '.on-row-del', function (e) {
        e.preventDefault();
    //    if (!confirm('Are you sure to delete the row?')) return;

        var $tr = $(this).closest('tr');
        var id = $tr.data('id');
        if ($tr.data('isnew')){
            //new row - remove
            $tr.remove();
        }else{
            //existing record - mark to delete
            $tr.prepend('<input type="hidden" name="del['+id+']" value="1">');
            $tr.slideUp();
        }
        $('#DataF .btn-primary').removeClass('disabled');
    });

    $(document).on('click', '.on-cancel', function (e) {
        //just re-read page
        window.location.reload();
    });

    $tedit.on('keyup', ':input', function (e) {
        $('#DataF .btn-primary').removeClass('disabled');
    });
    $tedit.on('click', 'input[type=checkbox]', function (e) {
        $('#DataF .btn-primary').removeClass('disabled');
    });
    $tedit.on('change', 'select', function (e) {
        $('#DataF .btn-primary').removeClass('disabled');
    });

    $(document).on('click', '.on-add-new', function (e) {
        e.preventDefault();
        add_new_row();
    });
    add_new_row(); //by default - add new empty row

    $tedit.find('input[type=text]:visible:first').focus();

}else{
    //list mode
    //make_table_list(".list");
}

function add_new_row(){
    //add empty row
    var nextid = $tedit.data('newid')+1;
    $tedit.data('newid', nextid);

    var $tpl = $tedit.find('.tpl').clone().removeClass('tpl hide');
    $tpl.data('id', nextid);
    $tpl.data('isnew', true);
    $tpl.find('input.rowid').prop('name', 'new['+nextid+']');
    $tpl.find('input[name^=fnew0]').each(function(index, el) {
        el.name = el.name.replace(/fnew0/,'fnew'+nextid);
    });
    $tedit.find('tbody').append($tpl);

    $tpl.find('input[type=text]:visible:first').focus();
}