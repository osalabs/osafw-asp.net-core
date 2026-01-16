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

window.dashboardChartOverrides = {
    themeColor: themeColor,
    palette: themePalette,
    bodyColor: bodyColor,
    paneBg: paneBg,
    borderColor: borderColor,
    fontFamily: fontFamily,
    barRadius: 6,
    barMaxWidth: 16,
    lineWidth: 4,
    lineSmooth: 0.4,
    areaOpacity: 0.35,
    pieBorderColor: is_dark_mode ? '#222' : '#fff',
    pieBorderWidth: 4
};
