$(function () {
  $('.fw-form-section-toggle').each(function () {
    var $btn = $(this);
    var target = $btn.data('bsTarget') || $btn.attr('data-bs-target');
    var $label = $btn.find('.fw-section-toggle-label');
    var showLabel = $label.data('label-show') || 'Show';
    var hideLabel = $label.data('label-hide') || 'Hide';

    if (!target) return;

    var $target = $(target);
    var updateLabel = function (isShown) {
      $label.text(isShown ? hideLabel : showLabel);
    };

    updateLabel($target.hasClass('show'));
    $target.on('shown.bs.collapse', function () { updateLabel(true); });
    $target.on('hidden.bs.collapse', function () { updateLabel(false); });
  });
});
