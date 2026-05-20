(function () {
  var timezone = '';

  try {
    timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || '';
  } catch (e) {
    timezone = '';
  }

  $('#timezone_auto').val(timezone);
})();
