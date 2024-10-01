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

let v='inline var';
console.log(`This is ${v}`);//no lang parsed in .js files