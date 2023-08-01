//$('form[data-autosave]').on('autosave-success', function(e, data) {
//    console.log('autosaved', data);
//});


//list check all/none handler
$(document).on('click', '.on-row-chkall', function (e){
    $(this).closest('tr').find('.row-cb').prop('checked', this.checked);
});

$(document).on('click', '.on-matrix-chkall', function (e){
    $(this).closest('table').find('.row-cb').prop('checked', this.checked);
});

$(document).on('click', '.on-col-chkall', function (e){
    // set checked for the column
    var is_checked = this.checked;
    var $td = $(this).closest('th');   
    var col_index = $td.index() + 2; // 0-firt permission col, so need +2 to get index of the tbody column
    $(this).closest('table').find('tbody tr').each(function () {
        $(this).find('td').eq(col_index).find('.row-cb').prop('checked', is_checked);
    });
});

