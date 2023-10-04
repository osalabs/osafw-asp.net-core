//blue theme
window.Chart.defaults = $.extend(true, window.Chart.defaults, {
    color: '#748194',
    font: {
        color: '#748194',
        size: 13,
        family: '',
    },
    elements: {
        point: {
            radius: 0,
            backgroundColor: '#0b1727'
        },
        bar: {
            backgroundColor: '#2c7be5'
        },
        line: {
            tension: 0.4,
            borderWidth: 3,
            borderColor: '#2c7be5',
            backgroundColor: '#2c7be5',
            fill: false,
            borderCapStyle: "rounded"
        },
        rectangle: {
            backgroundColor: '#2c7be5'
        },
        arc: {
            backgroundColor: '#0b1727',
            borderColor: '#ffffff',
            borderWidth: 4
        }
    },
    doughnut: {
        cutoutPercentage: 80,
        backgroundColor: [
            '#2c7be5',
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
                    color: '#dddddd',
                    drawBorder: false,
                    drawTicks: false,
                    lineWidth: 0,
                    zeroLineWidth: 0,
                    zeroLineColor: '#dddddd',
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
                    color: '#dddddd',
                    drawBorder: false,
                    drawTicks: false,
                    lineWidth: 0,
                    zeroLineWidth: 0,
                    zeroLineColor: '#dddddd',
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
