//blue theme
const styles = getComputedStyle(document.documentElement);
const themeColor = styles.getPropertyValue('--theme-color').trim() || '#6658dd';
const bodyColor = styles.getPropertyValue('--theme-body-color').trim() || '#aeb8c5';
const paneBg = styles.getPropertyValue('--fw-body-bg').trim() || '#6658dd';
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
            backgroundColor: '#313a46',
            borderColor: '#ffffff',
            borderWidth: 4
        }
    },
    doughnut: {
        cutoutPercentage: 80,
        backgroundColor: [
            themeColor,
            '#1abc9c',
            '#fa5c7c',
            '#4fc6e1',
            '#6c757d',
            '#f1f3fa',
            '#3688fc',
            '#dee2e6'
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