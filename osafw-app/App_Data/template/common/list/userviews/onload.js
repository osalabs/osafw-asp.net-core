var $modal = $('#modal-custom-cols');
//check all/none
$modal.find('#custom-cols-checkall').on('click', function (e) {
    $modal.find('.col-rows input[type="checkbox"]').prop('checked', $(this).prop('checked'));
});

$modal.on('click', '.cb-catrow > input', function (e) {
    $(this).closest('li').find('.col-rows input[type="checkbox"]').prop('checked', $(this).prop('checked'));
});


//sort by drag & drop
var $colrows = $modal.find(".col-rows");
$colrows.sortable();

// $colrows.on('sortchange', function( e, ui ) {
//     console.log('sortchange', e);
// });
// $colrows.on('sortstop', function( e, ui ) {
//     console.log('sortstop', e);
// });
$colrows.on('sortupdate', function( e, ui ) {
    //console.log('sortupdate', e, ui);
    //update values - just find all checkbox elements - jquery iterate it in order, so we only need to assign incrementing index
    update_indexes();
});

//update on load
update_indexes();

function update_indexes(){
    var index=1;
    $colrows.find('input[type="checkbox"]').each(function(i, el) {
        //console.log(el.name, index);
        el.value = index;
        index++;
    });
}