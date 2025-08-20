//blue theme
themeColor = styles.getPropertyValue('--theme-color').trim() || '#2c7be5';
bodyColor = styles.getPropertyValue('--theme-body-color').trim() || '#748194';
paneBg = styles.getPropertyValue('--fw-body-bg').trim() || '#0b1727';
borderColor = styles.getPropertyValue('--bs-border-color').trim() || '#dddddd';
fontFamily = styles.getPropertyValue('--bs-body-font-family').trim();

themePalette = [
    themeColor,
    '#27bcfd',
    '#00d27a',
    '#adb4c1',
    '#6ab1f2',
    '#63dbfe',
    '#00e8b1',
    '#d2d7de'
];

Chart.defaults.set({
    color: bodyColor,
    font: {
        color: bodyColor,
        size: 13,
        family: fontFamily,
    },
    datasets: {
        bar: {
            backgroundColor: themeColor,
            borderColor: themeColor,
            borderRadius: 5,
        },
        line: {
            borderColor: themeColor,
            backgroundColor: themeColor,
            borderWidth: 5,
            tension: 0.4,
            fill: true,
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
            borderWidth: 4
        }
    },
    doughnut: {
        backgroundColor: themePalette
    },
});

Chart.overrides.bar = {
    ...Chart.overrides.bar,
    maxBarThickness: 14,
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
                borderDashOffset: [2],
                color: borderColor,
                drawBorder: false,
                drawTicks: false,
                lineWidth: 0,
                zeroLineWidth: 0,
                zeroLineColor: borderColor,
                zeroLineBorderDash: [3],
                zeroLineBorderDashOffset: [2]
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
            },
        },
        y: {
            grid: {
                borderDash: [3],
                borderDashOffset: [2],
                color: borderColor,
                drawBorder: false,
                drawTicks: false,
                lineWidth: 0,
                zeroLineWidth: 0,
                zeroLineColor: borderColor,
                zeroLineBorderDash: [3],
                zeroLineBorderDashOffset: [2]
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