let is_dark_mode = document.documentElement.getAttribute('data-bs-theme') == 'dark';
const styles = getComputedStyle(document.documentElement);
let themeColor = '#007bff';
let bodyColor = '#999999';
let paneBg = '#333333';
let borderColor = styles.getPropertyValue('--bs-border-color').trim() || '#dddddd';
let fontFamily = styles.getPropertyValue('--bs-body-font-family').trim();

let themePalette = [
    themeColor,
    '#3295FF',
    '#66AFFF',
    '#99CAFF',
    '#B2D7FF',
    '#CCE4FF',
    '#E5F1FF',
    '#F2F8FF'
];

window.dashboardChartConfig = {
    themeColor: themeColor,
    palette: themePalette,
    bodyColor: bodyColor,
    borderColor: borderColor,
    paneBg: paneBg,
    fontFamily: fontFamily,
    fontSize: 13,
    barRadius: 10,
    barMaxWidth: 14,
    lineWidth: 3,
    lineSmooth: 0.35,
    areaOpacity: 0.28,
    pieHole: ['58%', '78%'],
    pieBorderColor: is_dark_mode ? '#222' : '#fff',
    pieBorderWidth: 2
};

window.applyDashboardChartOverrides = function (overrides) {
    if (!overrides) {
        return;
    }
    if (overrides.palette) {
        window.dashboardChartConfig.palette = overrides.palette;
    }
    Object.assign(window.dashboardChartConfig, overrides);
};

window.getDashboardChartBaseOptions = function () {
    const cfg = window.dashboardChartConfig;
    return {
        textStyle: {
            color: cfg.bodyColor,
            fontFamily: cfg.fontFamily,
            fontSize: cfg.fontSize
        },
        tooltip: {
            backgroundColor: is_dark_mode ? '#1b1f23' : '#ffffff',
            borderColor: cfg.borderColor,
            textStyle: {
                color: cfg.bodyColor
            }
        },
        legend: {
            textStyle: {
                color: cfg.bodyColor
            }
        }
    };
};

window.initDashboardChart = function (elementId, options) {
    if (!window.echarts) {
        return null;
    }
    const element = document.getElementById(elementId);
    if (!element) {
        return null;
    }
    const chart = echarts.init(element);
    chart.setOption(window.getDashboardChartBaseOptions());
    chart.setOption(options);
    window.addEventListener('resize', function () {
        chart.resize();
    });
    return chart;
};

<~theme1.js ifeq="GLOBAL[ui_theme]" value="1">
<~theme2.js ifeq="GLOBAL[ui_theme]" value="2">
<~theme20.js ifeq="GLOBAL[ui_theme]" value="20">
<~theme30.js ifeq="GLOBAL[ui_theme]" value="30">

window.applyDashboardChartOverrides(window.dashboardChartOverrides);
