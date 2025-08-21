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
Chart.defaults.plugins.colors = { enabled: false }; //disable built-in colors plugin as we use custom colors

Chart.defaults.set({
    responsive: true,
    maintainAspectRatio: false,
    color: bodyColor,
    font: {
        size: 15,
        family: fontFamily,
    },
    layout: {
        padding: 0
    },
    plugins:{
        legend: {
            display: false,
            position: "bottom",
            labels: {
                usePointStyle: true,
                padding: 16
            }
        },
    },
    datasets: {
        bar: {
            backgroundColor: themeColor,
            borderColor: themeColor,
            borderRadius: 20,
        },
        line: {
            borderColor: themeColor,
            backgroundColor: themeColor,
            borderWidth: 3,
            tension: 0.4,
            fill: false,
            borderCapStyle: "rounded",
        },
    },
    // in v3/v4, dataset-level defaults has priority over elements-level
    elements: {
        point: {
            radius: 0,
            backgroundColor: paneBg
        },
        rectangle: {
            backgroundColor: themeColor
        },
        arc: {
            backgroundColor: paneBg,
            borderColor: (is_dark_mode ? '#222' : '#fff'),
            borderWidth: 2
        }
    },

    doughnut: {
        backgroundColor: themePalette
    },
});

Chart.overrides.bar = {
    ...Chart.overrides.bar,
    maxBarThickness: 10,
    scales: {
        x: {
            grid: {
                drawBorder: false,
                drawOnChartArea: false,
                drawTicks: false
            },
            ticks: {
                padding: 10
            }
        },
        y: {
            grid: {
                borderDash: [3],
                borderDashOffset: 2,
                color: borderColor,
                drawBorder: false,
                drawTicks: false,
                lineWidth: 1,
            },
            beginAtZero: true,
            ticks: {
                padding: 5,
                callback: function(a) {
                    if ((a % 10)===0)
                        return a;
                }
            }
        }
    }
};

Chart.overrides.line = {
    ...Chart.overrides.line,
    scales: {
        x: {
            grid: {
                drawBorder: false,
                drawOnChartArea: false,
                drawTicks: false
            },
            ticks: {
                padding: 10
            }
        },
        y: {
            grid: {
                borderDash: [3],
                borderDashOffset: 2,
                color: borderColor,
                drawBorder: false,
                drawTicks: false,
                lineWidth: 1,
            },
            beginAtZero: true,
            ticks: {
                padding: 5,
                callback: function(a) {
                    if ((a % 10)===0)
                        return a;
                }
            }
        }
    }
};

<~theme1.js ifeq="GLOBAL[ui_theme]" value="1">
<~theme2.js ifeq="GLOBAL[ui_theme]" value="2">
<~theme20.js ifeq="GLOBAL[ui_theme]" value="20">
<~theme30.js ifeq="GLOBAL[ui_theme]" value="30">

//console.log(Chart.defaults);

