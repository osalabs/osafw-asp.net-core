hljs.highlightAll();

$('.on-apply-update').on('click', function(e) {
  e.preventDefault();
  e.stopPropagation();  
  const id = $(this).data("id");
  window.location.href = "<~../url>/(Apply)/" + id;
});

$('.on-views-update').on('click', function(e) {
  e.preventDefault();
  e.stopPropagation();  
  window.location.href = "<~../url>/(UpdateViews)";
});
