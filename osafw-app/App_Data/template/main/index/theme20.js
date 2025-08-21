//blue theme
themeColor = styles.getPropertyValue('--theme-color').trim() || '#8793FF';
bodyColor = styles.getPropertyValue('--theme-body-color').trim() || '#6c757d';
paneBg = styles.getPropertyValue('--fw-body-bg').trim() || '#f5f6f8';

themePalette = [
            themeColor,
            '#1abc9c',
            '#fa5c7c',
            '#4fc6e1',
            '#6c757d',
            '#f1f3fa',
            '#3688fc',
            '#dee2e6'
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
            borderRadius: 0,
        },
        line: {
            borderColor: themeColor,
            backgroundColor: themeColor,
            borderWidth: 3,
            tension: 0.2,
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
            backgroundColor: '#313a46',
            borderColor: (is_dark_mode ? '#444' : '#fff'),
            borderWidth: 4
        }
    },
    doughnut: {
        backgroundColor: themePalette
    }
});

Chart.overrides.bar = {
    ...Chart.overrides.bar,
    maxBarThickness: 14,
    scales: {
        x: {
            grid: {
                drawBorder: false,
                drawOnChartArea: true, // Enable grid lines
                drawTicks: false
            },
            ticks: {
                padding: 5
            }
        },
        y: {
            grid: {
                borderDash: [3],
                borderDashOffset: [2],
                color: borderColor,
                drawBorder: false,
                drawTicks: false,
                drawOnChartArea: true, // Enable grid lines
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
    Xscales: {
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