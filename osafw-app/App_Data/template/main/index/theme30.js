//blue theme
const styles = getComputedStyle(document.documentElement);
const themeColor = styles.getPropertyValue('--theme-color').trim() || '#2c7be5';
const bodyColor = styles.getPropertyValue('--theme-body-color').trim() || '#748194';
const paneBg = styles.getPropertyValue('--fw-body-bg').trim() || '#0b1727';
const borderColor = styles.getPropertyValue('--bs-border-color').trim() || '#dddddd';
const fontFamily = styles.getPropertyValue('--bs-body-font-family').trim();

window.Chart.defaults = $.extend(true, window.Chart.defaults, {
    color: bodyColor,
    font: {
        color: bodyColor,
        size: 13,
        family: fontFamily,
    },
    elements: {
        point: {
            radius: 0,
            backgroundColor: paneBg
        },
        bar: {
            backgroundColor: themeColor
        },
        line: {
            tension: 0.4,
            borderWidth: 3,
            borderColor: themeColor,
            backgroundColor: themeColor,
            fill: false,
            borderCapStyle: "rounded"
        },
        rectangle: {
            backgroundColor: themeColor
        },
        arc: {
            backgroundColor: paneBg,
            borderColor: '#ffffff',
            borderWidth: 4
        }
    },
    doughnut: {
        cutoutPercentage: 80,
        backgroundColor: [
            themeColor,
            '#27bcfd',
            '#00d27a',
            '#adb4c1',
            '#6ab1f2',
            '#63dbfe',
            '#00e8b1',
            '#d2d7de'
        ]
    }
});

window.Chart.overrides = $.extend(true, window.Chart.overrides, {
    bar: {
        maxBarThickness: 14,
        scales: {
            x: [{
                grid: {
                    drawBorder: false,
                    drawOnChartArea: false,
                    drawTicks: false
                },
                ticks: {
                    padding: 10
                }
            }],
            y: [{
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
                ticks: {
                    beginAtZero: true,
                    padding: 5,
                    callback: function(a) {
                        if ((a % 10)===0)
                            return a;
                    }
                }
            }]
        }
    },
    line: {
        maxBarThickness: 10,
        scales: {
            x: [{
                grid: {
                    drawBorder: false,
                    drawOnChartArea: false,
                    drawTicks: false
                },
                ticks: {
                    padding: 10
                },
            }],
            y: [{
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
                ticks: {
                    beginAtZero: true,
                    padding: 5,
                    callback: function(a) {
                        if ((a % 10)===0)
                            return a;
                    }
                }
            }]
        }
    }
});