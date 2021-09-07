$('#iname').on('keyup', function (e) {
    var title=$(this).val();
    //title=title.toLowerCase();
    title=title.replace(/^\W+/,'');
    title=title.replace(/\W+$/,'');
    title=title.replace(/\W+/g,'-');
    $('#url').val(title);
});