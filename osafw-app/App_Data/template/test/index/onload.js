$('.empty').each(function(){
    if ( $(this).html()>'' ){
        $(this).addClass('error');
    }
});

$('.true').each(function(){
    if ( $(this).html()=='' ){
        $(this).addClass('error').html('error');
    }
});