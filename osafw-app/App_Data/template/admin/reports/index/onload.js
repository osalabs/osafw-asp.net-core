const reportsFilterInput = document.getElementById('reportsFilter');

if (reportsFilterInput) {
  const items = Array.from(document.querySelectorAll('.js-report-item'));

  function normalize(value) {
    return (value || '').toLowerCase().replace(/[^a-z0-9]+/g, '');
  }

  function fuzzyMatch(needle, haystack) {
    if (!needle) return true;

    let index = 0;
    for (const char of haystack) {
      if (char === needle[index]) index++;
      if (index === needle.length) return true;
    }
    return false;
  }

  function filterReports() {
    const query = normalize(reportsFilterInput.value);

    items.forEach(function (item) {
      const title = normalize(item.dataset.reportTitle || item.textContent);
      item.hidden = !fuzzyMatch(query, title);
    });
  }

  reportsFilterInput.addEventListener('input', filterReports);
  reportsFilterInput.addEventListener('keyup', filterReports);
  filterReports();
}
