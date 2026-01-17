(function () {
  function initLastLoginCharts() {
    if (!window.fwEcharts || !window.echarts) {
      return;
    }
    document.querySelectorAll(".last-logins-chart").forEach(function (el) {
      var dataAttr = el.getAttribute("data-logins") || "";
      if (!dataAttr) {
        return;
      }
      var data;
      try {
        data = JSON.parse(dataAttr);
      } catch (err) {
        return;
      }
      if (!data || !data.values) {
        return;
      }
      window.fwEcharts.renderMiniBar(el, data.labels || [], data.values);
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initLastLoginCharts);
  } else {
    initLastLoginCharts();
  }
})();
