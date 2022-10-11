//dark theme
window.Chart.defaults = $.extend(true, window.Chart.defaults, {
    elements: {
        bar: {
            borderColor: '#000000',
            backgroundColor: '#000000',
        },
        line: {
            borderColor: '#000000',
            backgroundColor: '#000000',
        },
        rectangle: {
            backgroundColor: '#000000',
        }
    },
    doughnut: {
        backgroundColor: [
            '#7b7b7b',
            '#323232',
            '#666666',
            '#999999',
            '#B2B2B2',
            '#CCCCCC',
            '#E5E5E5',
            '#F2F2F2'
        ]
    }
});