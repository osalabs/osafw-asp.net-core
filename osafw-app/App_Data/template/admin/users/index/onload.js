(function () {
  if (!window.echarts) {
    return;
  }

  var charts = document.querySelectorAll('.last-logins-chart');
  if (!charts.length) {
    return;
  }

  var labels = [];
  for (var i = 6; i >= 0; i -= 1) {
    var d = new Date();
    d.setDate(d.getDate() - i);
    labels.push((d.getMonth() + 1) + '/' + d.getDate());
  }

  charts.forEach(function (element) {
    var raw = element.getAttribute('data-values') || '';
    var values = raw.split(',').map(function (value) {
      var parsed = parseInt(value, 10);
      return Number.isFinite(parsed) ? parsed : 0;
    });

    while (values.length < 7) {
      values.unshift(0);
    }
    if (values.length > 7) {
      values = values.slice(values.length - 7);
    }

    var chart = echarts.init(element, null, { renderer: 'canvas' });
    chart.setOption({
      animation: false,
      grid: {
        left: 2,
        right: 2,
        top: 2,
        bottom: 2
      },
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'line' },
        formatter: function (params) {
          if (!params || !params.length) {
            return '';
          }
          var item = params[0];
          return labels[item.dataIndex] + ': ' + item.value;
        }
      },
      xAxis: {
        type: 'category',
        data: labels,
        show: false
      },
      yAxis: {
        type: 'value',
        show: false,
        minInterval: 1
      },
      series: [
        {
          type: 'line',
          data: values,
          smooth: true,
          symbol: 'none',
          lineStyle: {
            color: '#0d6efd',
            width: 2
          },
          areaStyle: {
            color: 'rgba(13, 110, 253, 0.15)'
          }
        }
      ]
    });
  });
})();
