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

window.dashboardChartOverrides = {
    themeColor: themeColor,
    palette: themePalette,
    bodyColor: bodyColor,
    paneBg: paneBg,
    barRadius: 0,
    barMaxWidth: 16,
    lineSmooth: 0.2,
    pieBorderColor: is_dark_mode ? '#444' : '#fff',
    pieBorderWidth: 4,
    pieCornerRadius: 0,
    pieShowLegend: true,
    pieShowLabels: false
};
