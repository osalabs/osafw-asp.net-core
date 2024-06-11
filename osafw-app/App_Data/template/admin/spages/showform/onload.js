// make sure we only auto-update url from title if user didn't set it manually
$('#iname').on('keyup', function (e) {
    var $url = $('#url');
    if (!$url.data('changed')){
        //if url not yet changed - check if it's filled
        if ($url.val()>''){
            //do not auto-change if url filled
            return;
        }else{
            //if empty - can be changed
            $url.data('changed', true);
        }
    }

    var title=$(this).val();
    //title=title.toLowerCase();
    title=title.replace(/^\W+/,'');
    title=title.replace(/\W+$/,'');
    title=title.replace(/\W+/g,'-');
    $('#url').val(title);
});

$('#url').on('change', function(e){
    //user changing url manually - reset changed flag,
    //so if user cleared url - it can be auto-updated, othewrise - not
    var $this=$(this);
    $this.data('changed', false);
});