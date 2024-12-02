$('.on-apply-update').on('click', function(e) {
  e.preventDefault();
  e.stopPropagation();  
  const id = $(this).data("id");
  const form = $('#FApplyUpdate');
  form.attr('action',"<~../url>/(Save)/" + id);
  form.submit();

});

<~../highlight.js>
highlightSQL();
