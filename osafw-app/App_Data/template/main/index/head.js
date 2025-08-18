var is_dark_mode = document.documentElement.getAttribute('data-bs-theme')=='dark';
Chart.defaults.set({
    responsive: true,
    maintainAspectRatio: false,
    color: '#999999',
    font: {
        size: 15,
        family: '',
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
    elements: {
        point: {
            radius: 0,
            backgroundColor: '#333333'
        },
        bar: {
            backgroundColor: '#007bff'
        },
        line: {
            tension: 0.4,
            borderWidth: 3,
            borderColor: '#007bff',
            backgroundColor: '#007bff',
            fill: false,
            borderCapStyle: "rounded",
        },
        rectangle: {
            backgroundColor: '#007bff'
        },
        arc: {
            backgroundColor: '#333333',
            borderColor: (is_dark_mode ? '#222' : '#fff'),
            borderWidth: 2
        }
    },

    doughnut: {
        backgroundColor: [
            '#007bff',
            '#3295FF',
            '#66AFFF',
            '#99CAFF',
            '#B2D7FF',
            '#CCE4FF',
            '#E5F1FF',
            '#F2F8FF'
        ]
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
                color: '#dddddd',
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
                color: '#dddddd',
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
<~theme30.js ifeq="GLOBAL[ui_theme]" value="30">

//console.log(Chart.defaults);

