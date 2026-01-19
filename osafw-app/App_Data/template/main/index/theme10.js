//pink theme
themeColor = styles.getPropertyValue('--bs-primary').trim() || '#e75b98';
bodyColor = styles.getPropertyValue('--bs-body-color').trim() || '#5b4a55';
paneBg = styles.getPropertyValue('--fw-body-bg').trim() || '#f9f1f6';
borderColor = styles.getPropertyValue('--bs-border-color').trim() || '#edd7e3';
fontFamily = styles.getPropertyValue('--bs-body-font-family').trim();

themePalette = [
    themeColor,
    '#f2a6c8',
    '#f7c3d9',
    '#f4b267',
    '#4fbf9b',
    '#6f9df9',
    '#e65f6c',
    '#bfa6b4'
];

window.dashboardChartOverrides = {
    themeColor: themeColor,
    palette: themePalette,
    bodyColor: bodyColor,
    paneBg: paneBg,
    barRadius: 0,
    barMaxWidth: 16,
    lineSmooth: 0.2,
    pieBorderColor: is_dark_mode ? '#2b1a25' : '#fff',
    pieBorderWidth: 4,
    pieCornerRadius: 0,
    pieShowLegend: true,
    pieShowLabels: false
};
